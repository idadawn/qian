using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Qian.Gateway.Tests;

// 外壳静态托管（S1-YUE-012）：入口与深链接 no-store、/assets 带哈希资源长缓存、远程制品按组件目录、运行时配置每次现读，全部响应带同一组安全头。
public sealed class GatewayShellHostingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 外壳入口与深链接不缓存_带哈希资源长缓存_远程制品按组件目录_运行时配置每次现读_非页面导航不回入口_安全头齐全()
    {
        var root = Directory.CreateTempSubdirectory("qian-shell-static-");
        try
        {
            var dist = Write(root, "dist/index.html", "<!doctype html><title>shell</title>");
            Write(root, "dist/assets/index-Ab12Cd34.js", "export {};");
            Write(root, "dist/favicon.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
            var remote = Write(root, "remotes/crm/mf-manifest.json", "{\"id\":\"qian.sample.crm\"}");
            var config = Path.Combine(root.FullName, "config", "app-config.json");
            Write(root, "config/app-config.json", "{\"apiBase\":\"/api\"}");
            var options = new ShellStaticOptions { Root = dist, ConfigFile = config, Remotes = new Dictionary<string, string> { ["qian.sample.crm"] = remote } };
            var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            await using var app = builder.Build();
            app.UseQianShell(options); app.UseRouting();
            app.MapGet("/api/ping", () => "pong"); app.MapQianShell(options);
            await app.StartAsync(Ct);
            using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

            foreach (var path in new[] { "/", "/customers", "/customers/new", "/customers?x=1" })
            {
                using var page = await Navigate(http, HttpMethod.Get, path);
                await AssertBody(page, HttpStatusCode.OK, "no-store", "<!doctype html><title>shell</title>");
                Assert.Equal("text/html", page.Content.Headers.ContentType?.MediaType);
            }
            using (var head = await Navigate(http, HttpMethod.Head, "/customers")) await AssertBody(head, HttpStatusCode.OK, "no-store", "");
            using (var html = await http.GetAsync("/index.html", Ct)) await AssertBody(html, HttpStatusCode.OK, "no-store", "<!doctype html><title>shell</title>");
            using (var asset = await http.GetAsync("/assets/index-Ab12Cd34.js", Ct)) await AssertBody(asset, HttpStatusCode.OK, "public, max-age=31536000, immutable", "export {};");
            using (var icon = await http.GetAsync("/favicon.svg", Ct)) await AssertBody(icon, HttpStatusCode.OK, "no-cache", "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
            using (var manifest = await http.GetAsync("/remotes/qian.sample.crm/mf-manifest.json", Ct)) await AssertBody(manifest, HttpStatusCode.OK, "no-cache", "{\"id\":\"qian.sample.crm\"}");
            // 不存在的资源与未部署的组件是 404，不是入口页（脚本请求拿到 HTML 会以错误的 MIME 执行失败）。
            foreach (var path in new[] { "/assets/missing.js", "/remotes/qian.sample.crm/missing.js", "/remotes/other/mf-manifest.json" })
                using (var missing = await http.GetAsync(path, Ct)) Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            // 非页面导航（fetch 的 JSON 请求、其它方法）不回入口：API 前缀下打错的路径是 404，不拿 HTML 冒充 JSON。
            using (var json = await http.GetAsync("/api/typo", Ct)) await AssertBody(json, HttpStatusCode.NotFound, "no-store", "");
            using (var post = await Navigate(http, HttpMethod.Post, "/customers")) await AssertBody(post, HttpStatusCode.NotFound, "no-store", "");
            using (var api = await http.GetAsync("/api/ping", Ct)) Assert.Equal("pong", await api.Content.ReadAsStringAsync(Ct));

            // 运行时配置：每次现读挂载文件（ConfigMap 更新后刷新即生效），no-store；文件不在即 404。
            using (var first = await http.GetAsync("/app-config.json", Ct)) await AssertBody(first, HttpStatusCode.OK, "no-store", "{\"apiBase\":\"/api\"}");
            await File.WriteAllTextAsync(config, "{\"apiBase\":\"/api-alt\"}", Ct);
            using (var second = await http.GetAsync("/app-config.json", Ct))
            {
                await AssertBody(second, HttpStatusCode.OK, "no-store", "{\"apiBase\":\"/api-alt\"}");
                Assert.Equal("application/json; charset=utf-8", second.Content.Headers.ContentType?.ToString());
            }
            using (var head = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/app-config.json"), Ct)) await AssertBody(head, HttpStatusCode.OK, "no-store", "");
            File.Delete(config);
            using (var gone = await http.GetAsync("/app-config.json", Ct)) await AssertBody(gone, HttpStatusCode.NotFound, "no-store", "");
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public void 外壳目录缺入口_远程制品键不是组件id或目录不存在_拒绝启动()
    {
        var root = Directory.CreateTempSubdirectory("qian-shell-static-");
        try
        {
            var dist = Write(root, "dist/index.html", "<!doctype html>");
            var remote = Write(root, "remotes/crm/mf-manifest.json", "{}");
            var app = WebApplication.CreateBuilder().Build();
            Assert.Throws<ArgumentException>(() => app.UseQianShell(new ShellStaticOptions { Root = Path.Combine(root.FullName, "remotes") }));
            Assert.Throws<ArgumentException>(() => app.UseQianShell(new ShellStaticOptions { Root = dist, Remotes = new Dictionary<string, string> { ["../crm"] = remote } }));
            Assert.Throws<ArgumentException>(() => app.UseQianShell(new ShellStaticOptions { Root = dist, Remotes = new Dictionary<string, string> { ["qian.sample.crm"] = Path.Combine(root.FullName, "nope") } }));
            Assert.Throws<ArgumentException>(() => app.MapQianShellBootstrap("https://evil.test/bootstrap"));
            Assert.Throws<ArgumentException>(() => app.MapQianShellBootstrap("/api/shell/bootstrap/"));
        }
        finally { root.Delete(recursive: true); }
    }

    private static Task<HttpResponseMessage> Navigate(HttpClient http, HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        return http.SendAsync(request, Ct);
    }

    private static async Task AssertBody(HttpResponseMessage response, HttpStatusCode status, string cacheControl, string body)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(cacheControl, string.Join(", ", response.Headers.GetValues("Cache-Control")));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal("frame-ancestors 'none'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal(body, await response.Content.ReadAsStringAsync(Ct));
    }

    // 写一个文件并返回它所在的目录。
    private static string Write(DirectoryInfo root, string relative, string content)
    {
        var path = Path.Combine(root.FullName, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return Path.GetDirectoryName(path)!;
    }
}
