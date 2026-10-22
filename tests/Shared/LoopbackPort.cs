using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;

namespace Qian.Testing;

// 测试宿主的回环端口（S1-QA-014）。发行方地址里带端口，必须在构建宿主前确定，所以只能先向系统要一个空闲端口、放掉，
// 再在建表、挂载组件之后交给 Kestrel 绑定。这几秒里端口可能被占走：并行的测试进程（--max-parallel-test-modules），
// 或者容器的宿主端口映射（同样取自临时端口段）。绑定失败就换一个端口、从头启动宿主，最多 3 次；其余失败照常抛出。
// 绑定失败也不能换端口的宿主（地址已登记到别的宿主）改用 Reserve：一直占住端口，绑定前一刻才释放（S1-QA-016）。
internal static class LoopbackPort
{
    internal const int Attempts = 3;

    /// <summary>
    /// 以新取的空闲回环端口调用 <paramref name="start"/>；Kestrel 绑定时端口已被占（<see cref="AddressInUseException"/>）就换端口重来。
    /// <paramref name="start"/> 失败时须先释放本次创建的宿主与资源再抛出。
    /// </summary>
    internal static async Task<T> StartAsync<T>(Func<int, Task<T>> start)
    {
        for (var attempt = 1; ; attempt++)
        {
            var port = Free();
            try { return await start(port); }
            catch (IOException error) when (attempt < Attempts && error.InnerException is AddressInUseException)
            {
                TestContext.Current.SendDiagnosticMessage($"回环端口 {port} 在绑定前被占用，第 {attempt} 次换端口重启宿主。");
            }
        }
    }

    /// <summary>
    /// 占住一个空闲回环端口，直到释放返回值：IPv4 与 IPv6 两个回环地址都占（Kestrel 的 localhost 绑定两个都要），本机没有 IPv6 回环时只占 IPv4。
    /// 用于地址要先登记到别的宿主、本宿主稍后才绑定的情形，例如依赖方的回调地址先登记到发行方。占住期间别的进程拿不到它，释放后立即绑定。
    /// </summary>
    internal static Reservation Reserve()
    {
        for (var attempt = 1; ; attempt++)
        {
            var v4 = new TcpListener(IPAddress.Loopback, 0);
            TcpListener? v6 = null;
            try
            {
                v4.Start();
                var port = ((IPEndPoint)v4.LocalEndpoint).Port;
                v6 = new TcpListener(IPAddress.IPv6Loopback, port);
                try { v6.Start(); }
                catch (SocketException error) when (error.SocketErrorCode is SocketError.AddressFamilyNotSupported or SocketError.AddressNotAvailable)
                {
                    v6.Dispose(); v6 = null;
                }
                return new Reservation(port, v4, v6);
            }
            // IPv4 空闲、IPv6 同号却已被占：换一个端口。
            catch (SocketException error) when (attempt < Attempts && error.SocketErrorCode == SocketError.AddressAlreadyInUse) { v4.Dispose(); v6?.Dispose(); }
            catch { v4.Dispose(); v6?.Dispose(); throw; }
        }
    }

    private static int Free()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }

    /// <summary><see cref="Reserve"/> 占住的端口；Dispose 即释放，可重复调用。</summary>
    internal sealed class Reservation(int port, TcpListener v4, TcpListener? v6) : IDisposable
    {
        internal int Port => port;

        public void Dispose() { v4.Dispose(); v6?.Dispose(); }
    }
}
