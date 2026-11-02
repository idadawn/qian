using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Qian.Dev.Shell;

namespace Qian.Tenancy.Signup.Tests;

// 公开页托管扩展与开发宿主的开发期发件箱：真实 Kestrel，回环地址。
public sealed class SignupHostingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 公开页同源托管_页面路由回入口且不缓存_带哈希资源长缓存_不存在的资源404_安全头齐全()
    {
        var root = Directory.CreateTempSubdirectory("qian-signup-pages-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root.FullName, "index.html"), "<!doctype html><title>signup</title>", Ct);
            Directory.CreateDirectory(Path.Combine(root.FullName, "assets"));
            await File.WriteAllTextAsync(Path.Combine(root.FullName, "assets", "app-1a2b.js"), "export {};", Ct);
            Assert.Throws<ArgumentException>(() => WebApplication.CreateBuilder().Build().UseQianTenancySignupPages(Path.Combine(root.FullName, "missing")));
            var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            await using var app = builder.Build();
            app.UseQianTenancySignupPages(root.FullName);
            app.MapGet("/other", () => "other");
            await app.StartAsync(Ct);
            using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            foreach (var page in new[] { "/signup", "/signup/", "/signup/status", "/signup/status/AP-20261103-0001" })
            {
                using var response = await client.GetAsync(page, Ct);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Contains("<title>signup</title>", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
                Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
                Headers(response);
            }
            using (var asset = await client.GetAsync("/signup/assets/app-1a2b.js", Ct))
            {
                Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
                Assert.Equal("public, max-age=31536000, immutable", asset.Headers.CacheControl!.ToString());
                Headers(asset);
            }
            using (var missing = await client.GetAsync("/signup/assets/missing.js", Ct))
            { Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode); Headers(missing); }
            using (var other = await client.GetAsync("/other", Ct)) Assert.Equal("other", await other.Content.ReadAsStringAsync(Ct));
            await app.StopAsync(Ct);
        }
        finally { root.Delete(true); }

        static void Headers(HttpResponseMessage response)
        {
            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
            Assert.Equal("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        }
    }

    [Fact]
    public void 开发期发件箱只在Development环境且只在回环宿主上登记()
    {
        var token = DevSignupOutbox.NewToken();
        Assert.Throws<InvalidOperationException>(() => new DevSignupOutbox(new Environment("Production"), [new Uri("http://127.0.0.1:5090/")], token));
        Assert.Throws<InvalidOperationException>(() => new DevSignupOutbox(new Environment("Staging"), [new Uri("http://127.0.0.1:5090/")], token));
        Assert.Throws<InvalidOperationException>(() => new DevSignupOutbox(new Environment("Development"), [new Uri("http://0.0.0.0:5090/")], token));
        Assert.Throws<InvalidOperationException>(() => new DevSignupOutbox(new Environment("Development"), [new Uri("http://127.0.0.1:5090/"), new Uri("http://10.0.0.8:5090/")], token));
        Assert.Throws<InvalidOperationException>(() => new DevSignupOutbox(new Environment("Development"), [], token));
        Assert.Throws<ArgumentException>(() => new DevSignupOutbox(new Environment("Development"), [new Uri("http://127.0.0.1:5090/")], "short"));
        _ = new DevSignupOutbox(new Environment("Development"), [new Uri("http://127.0.0.1:5090/"), new Uri("http://localhost:5091/"), new Uri("http://[::1]:5092/")], token);
    }

    [Fact]
    public async Task 开发期发件箱的读取路由只答回环连接且要开发令牌_按消息Id去重_新的在前()
    {
        var token = DevSignupOutbox.NewToken();
        var outbox = new DevSignupOutbox(new Environment("Development"), [new Uri("http://127.0.0.1:5090/")], token);
        var first = new SignupVerificationMessage(Guid.NewGuid(), "+8613912345678", "123456", DateTimeOffset.UtcNow.AddMinutes(5));
        var second = new SignupVerificationMessage(Guid.NewGuid(), "+8613912345678", "654321", DateTimeOffset.UtcNow.AddMinutes(5));
        await outbox.SendVerificationCodeAsync(first, Ct); await outbox.SendVerificationCodeAsync(first, Ct); await outbox.SendVerificationCodeAsync(second, Ct);
        var result = new SignupResultMessage(Guid.NewGuid(), "+8613912345678", "AP-20261103-0001", SignupResultKind.Rejected) { Reason = "资料不全" };
        await outbox.SendResultAsync(result, Ct); await outbox.SendResultAsync(result, Ct);

        Assert.Equal(404, await Status(Request(IPAddress.Parse("10.1.2.3"), IPAddress.Loopback, "127.0.0.1", token, "13912345678")));
        Assert.Equal(404, await Status(Request(IPAddress.Loopback, IPAddress.Parse("10.1.2.3"), "127.0.0.1", token, "13912345678")));
        Assert.Equal(404, await Status(Request(IPAddress.Loopback, IPAddress.Loopback, "evil.example", token, "13912345678")));
        Assert.Equal(404, await Status(Request(IPAddress.Loopback, IPAddress.Loopback, "127.0.0.1", null, "13912345678")));
        Assert.Equal(404, await Status(Request(IPAddress.Loopback, IPAddress.Loopback, "127.0.0.1", DevSignupOutbox.NewToken(), "13912345678")));
        Assert.Equal(400, await Status(Request(IPAddress.Loopback, IPAddress.Loopback, "127.0.0.1", token, "12")));

        foreach (var phone in new[] { "13912345678", "+8613912345678", " 8613912345678" })
        {
            var http = Request(IPAddress.IPv6Loopback, IPAddress.IPv6Loopback, "localhost", token, phone);
            await outbox.Read(http).ExecuteAsync(http);
            Assert.Equal(200, http.Response.StatusCode);
            http.Response.Body.Position = 0;
            using var body = await JsonDocument.ParseAsync(http.Response.Body, cancellationToken: Ct);
            Assert.Equal("+8613912345678", body.RootElement.GetProperty("phone").GetString());
            Assert.Equal(["654321", "123456"], body.RootElement.GetProperty("codes").EnumerateArray().Select(item => item.GetProperty("code").GetString()));
            var notification = Assert.Single(body.RootElement.GetProperty("notifications").EnumerateArray());
            Assert.Equal(("rejected", "AP-20261103-0001", "资料不全"), (notification.GetProperty("kind").GetString(), notification.GetProperty("applicationNumber").GetString(),
                notification.GetProperty("reason").GetString()));
        }
        Assert.Equal("+8613912345678", DevSignupOutbox.Normalize("139 1234 5678"));
        Assert.Equal("+442079460958", DevSignupOutbox.Normalize("+44 20 7946 0958"));
        Assert.Null(DevSignupOutbox.Normalize("12"));

        async Task<int> Status(DefaultHttpContext http) { await outbox.Read(http).ExecuteAsync(http); return http.Response.StatusCode; }
    }

    private static DefaultHttpContext Request(IPAddress remote, IPAddress local, string host, string? token, string phone)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddOptions();
        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        http.Connection.RemoteIpAddress = remote; http.Connection.LocalIpAddress = local;
        http.Request.Method = "GET"; http.Request.Host = new HostString(host, 5090); http.Request.Path = DevSignupOutbox.Route;
        http.Request.QueryString = QueryString.Create("phone", phone);
        if (token is not null) http.Request.Headers[DevSignupOutbox.TokenHeader] = token;
        http.Response.Body = new MemoryStream();
        return http;
    }

    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "qian.tenancy.signup.tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
