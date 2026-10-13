using System.Net;
using System.Net.Sockets;

namespace Qian.Data.AspNetCore.Tests;

/// <summary>
/// 本机回环上的假服务端，模拟连通测试会遇到的异常对端。每个连入的套接字按给定脚本应答，然后等对方先关闭，
/// 让驱动读到完整应答，而不是被连接重置抢先打断。
/// </summary>
internal sealed class FakeServer : IDisposable
{
    private const int SslRequest = 80877103;
    private const int GssEncRequest = 80877104;
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();

    private FakeServer(Func<Socket, CancellationToken, Task> respond)
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = AcceptAsync(respond);
    }

    public int Port { get; }

    /// <summary>不讲 PostgreSQL 协议的服务（如 HTTP）：一连上就回一段 HTTP 响应。</summary>
    public static FakeServer Http() => new((socket, cancellationToken) =>
        socket.SendAsync("HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray(), cancellationToken).AsTask());

    /// <summary>讲 PostgreSQL 协议，但拒绝加密协商，并在启动消息后要求 Kerberos V5 认证（驱动不支持）。</summary>
    public static FakeServer KerberosOnly() => new(async (socket, cancellationToken) =>
    {
        while (true)
        {
            var header = new byte[4];
            await ReceiveExactlyAsync(socket, header, cancellationToken);
            var body = new byte[ReadInt32(header) - 4];
            await ReceiveExactlyAsync(socket, body, cancellationToken);
            if (ReadInt32(body) is SslRequest or GssEncRequest)
            {
                await socket.SendAsync("N"u8.ToArray(), cancellationToken);
                continue;
            }
            // AuthenticationKerberosV5：'R'、长度 8、认证码 2。
            await socket.SendAsync(new byte[] { (byte)'R', 0, 0, 0, 8, 0, 0, 0, 2 }, cancellationToken);
            return;
        }
    });

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        _stop.Dispose();
    }

    private async Task AcceptAsync(Func<Socket, CancellationToken, Task> respond)
    {
        var cancellationToken = _stop.Token;
        while (!cancellationToken.IsCancellationRequested)
        {
            Socket socket;
            try { socket = await _listener.AcceptSocketAsync(cancellationToken); }
            catch (Exception error) when (error is OperationCanceledException or SocketException or ObjectDisposedException) { return; }
            _ = ServeAsync(socket, respond, cancellationToken);
        }
    }

    private static async Task ServeAsync(Socket socket, Func<Socket, CancellationToken, Task> respond, CancellationToken cancellationToken)
    {
        using (socket)
        {
            try
            {
                await respond(socket, cancellationToken);
                // 应答后读到对方关闭为止。
                var drain = new byte[256];
                while (await socket.ReceiveAsync(drain, cancellationToken) > 0) { }
            }
            catch (Exception error) when (error is OperationCanceledException or SocketException or ObjectDisposedException or EndOfStreamException) { }
        }
    }

    private static int ReadInt32(byte[] bytes) => (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];

    private static async Task ReceiveExactlyAsync(Socket socket, byte[] buffer, CancellationToken cancellationToken)
    {
        for (var read = 0; read < buffer.Length;)
        {
            var count = await socket.ReceiveAsync(buffer.AsMemory(read), cancellationToken);
            if (count == 0) throw new EndOfStreamException();
            read += count;
        }
    }
}
