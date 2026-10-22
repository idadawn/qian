using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Qian.Testing;

namespace Qian.Identity.Server.Tests;

// S1-QA-014：测试宿主先取空闲端口、放掉，再在建表与挂载组件之后交给 Kestrel 绑定。这里用真实 Kestrel 模拟两步之间端口被别的进程占走。
public sealed class LoopbackPortTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 绑定前端口被占时换端口从头启动_每次都被占时第三次原样抛出_与端口无关的失败不重试()
    {
        // 第一次启动期间端口被别的进程占走（并一直占着），Kestrel 绑定失败；宿主释放后换到新端口，第二次成功并能访问。
        var ports = new List<int>();
        TcpListener? squatter = null;
        try
        {
            await using var app = await LoopbackPort.StartAsync(async port =>
            {
                ports.Add(port);
                squatter ??= Occupy(port);
                return await Host(port);
            });
            Assert.Equal(2, ports.Count); Assert.NotEqual(ports[0], ports[1]);
            using var http = new HttpClient();
            using var response = await http.GetAsync(new Uri($"http://127.0.0.1:{ports[1]}/ping"), Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally { squatter?.Dispose(); }

        // 每次都被占：试满 3 次，最后一次的绑定异常原样抛出。
        var attempts = 0;
        var error = await Assert.ThrowsAsync<IOException>(() => LoopbackPort.StartAsync(async port =>
        {
            attempts++;
            using var squatter = Occupy(port);
            return await Host(port);
        }));
        Assert.Equal(3, attempts); Assert.IsType<AddressInUseException>(error.InnerException);

        // 与端口无关的失败（例如组件挂载失败）不重试。
        attempts = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => LoopbackPort.StartAsync<WebApplication>(_ =>
        {
            attempts++;
            throw new InvalidOperationException("组件挂载失败");
        }));
        Assert.Equal(1, attempts);
    }

    private static TcpListener Occupy(int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        return listener;
    }

    // 与真实测试宿主同一约定：启动失败先释放这次建的宿主，再抛出。
    private static async Task<WebApplication> Host(int port)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}"); builder.Logging.ClearProviders();
        var app = builder.Build();
        app.MapGet("/ping", () => Results.Ok());
        try { await app.StartAsync(Ct); return app; }
        catch { await app.DisposeAsync(); throw; }
    }
}
