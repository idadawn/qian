using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Qian.Identity.Server.Tests;

// 认证页族与身份服务同源托管（S1-YUE-012）：/login 下的页面路由回入口且不缓存、/login/assets 长缓存、缺失资源 404，全部响应带同一组安全头；
// 选择页（prompt=select_account）的路径必须落在托管路径下。
public sealed class IdentityPagesHostingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 认证页族页面路由回入口不缓存_带哈希资源长缓存_缺失资源404_其它路径不接管_安全头齐全()
    {
        var root = Directory.CreateTempSubdirectory("qian-identity-pages-");
        try
        {
            Write(root, "index.html", "<!doctype html><title>login</title>");
            Write(root, "assets/index-Zx98Yw76.js", "export {};");
            Write(root, "favicon.svg", "<svg/>");
            var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            builder.Services.AddSingleton(new IdentityServerOptions { Issuer = new Uri("https://id.qian.test/") });
            await using var app = builder.Build();
            app.UseQianIdentityPages(root.FullName);
            app.MapGet("/login-status", () => "next");
            await app.StartAsync(Ct);
            using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            foreach (var path in new[] { "/login", "/login/", "/login/select-tenant?return_url=%2Fconnect%2Fauthorize", "/login/connector/dex?flow=abc", "/login/index.html" })
                using (var page = await http.GetAsync(path, Ct))
                {
                    await AssertBody(page, HttpStatusCode.OK, "no-store", "<!doctype html><title>login</title>");
                    Assert.Equal("text/html", page.Content.Headers.ContentType?.MediaType);
                }
            using (var head = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/login/select-tenant"), Ct)) await AssertBody(head, HttpStatusCode.OK, "no-store", "");
            using (var asset = await http.GetAsync("/login/assets/index-Zx98Yw76.js", Ct)) await AssertBody(asset, HttpStatusCode.OK, "public, max-age=31536000, immutable", "export {};");
            using (var icon = await http.GetAsync("/login/favicon.svg", Ct)) await AssertBody(icon, HttpStatusCode.OK, "no-cache", "<svg/>");
            // 带扩展名却不存在的资源不回入口页（脚本请求拿到 HTML 会以错误的 MIME 执行失败）。
            foreach (var path in new[] { "/login/assets/missing.js", "/login/missing.css" })
                using (var missing = await http.GetAsync(path, Ct)) await AssertBody(missing, HttpStatusCode.NotFound, "no-store", "");
            // 段边界之外与非 GET / HEAD 不接管，交给后续管线。
            using (var other = await http.GetAsync("/login-status", Ct)) Assert.Equal("next", await other.Content.ReadAsStringAsync(Ct));
            using (var post = await http.PostAsync("/login", new StringContent(""), Ct)) Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        }
        finally { root.Delete(recursive: true); }
    }

    [Fact]
    public void 选择页不在托管路径下或目录缺入口_拒绝启动()
    {
        var root = Directory.CreateTempSubdirectory("qian-identity-pages-");
        try
        {
            Write(root, "index.html", "<!doctype html>");
            var outside = WebApplication.CreateBuilder();
            outside.Services.AddSingleton(new IdentityServerOptions { Issuer = new Uri("https://id.qian.test/"), Accounts = new IdentityAccountOptions { SelectionPath = "/select-tenant" } });
            Assert.Throws<InvalidOperationException>(() => outside.Build().UseQianIdentityPages(root.FullName));
            var empty = WebApplication.CreateBuilder();
            empty.Services.AddSingleton(new IdentityServerOptions { Issuer = new Uri("https://id.qian.test/") });
            Assert.Throws<ArgumentException>(() => empty.Build().UseQianIdentityPages(Path.Combine(root.FullName, "missing")));
        }
        finally { root.Delete(recursive: true); }
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

    private static void Write(DirectoryInfo root, string relative, string content)
    {
        var path = Path.Combine(root.FullName, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
