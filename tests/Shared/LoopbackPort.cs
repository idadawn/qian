using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;

namespace Qian.Testing;

// 测试宿主的回环端口（S1-QA-014）。发行方地址里带端口，必须在构建宿主前确定，所以只能先向系统要一个空闲端口、放掉，
// 再在建表、挂载组件之后交给 Kestrel 绑定。这几秒里端口可能被占走：并行的测试进程（--max-parallel-test-modules），
// 或者容器的宿主端口映射（同样取自临时端口段）。绑定失败就换一个端口、从头启动宿主，最多 3 次；其余失败照常抛出。
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

    private static int Free()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }
}
