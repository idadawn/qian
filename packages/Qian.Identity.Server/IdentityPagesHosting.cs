using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace Qian.Identity.Server;

/// <summary>认证页族（packages/qian-identity-ui 的构建产物）与身份服务同源的静态托管（S1-YUE-012）。</summary>
public static class IdentityPagesHosting
{
    /// <summary>认证页族的托管路径（构建产物的 Vite base 为 <c>/login/</c>）；账号 Cookie scheme 的 LoginPath 应指向它。</summary>
    public const string PagesPath = "/login";

    /// <summary>
    /// 放在 <see cref="IdentityServerHosting.UseQianIdentityServer"/> 之后、UseRouting 之前：同源托管认证页族。<c>/login</c> 下的资源由静态文件提供
    /// （<c>/login/assets/</c> 带哈希长缓存），不带扩展名的页面路由（<c>/login</c>、<c>/login/select-tenant</c>、<c>/login/connector/{connectorId}</c> 等）
    /// 一律回入口 index.html；HTML 不缓存，全部响应带 nosniff、<c>Referrer-Policy: no-referrer</c> 与 <c>Content-Security-Policy: frame-ancestors 'none'</c>
    /// （页面不许被任何来源嵌入）。<c>Accounts.SelectionPath</c> 必须落在 <c>/login</c> 下，否则 <c>prompt=select_account</c> 会跳到未托管的页面，抛出。
    /// </summary>
    public static IApplicationBuilder UseQianIdentityPages(this IApplicationBuilder app, string root)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (string.IsNullOrWhiteSpace(root) || !File.Exists(Path.Combine(root, "index.html"))) throw new ArgumentException("认证页族目录必须存在且含 index.html。", nameof(root));
        var options = app.ApplicationServices.GetRequiredService<IdentityServerOptions>();
        if (!new PathString(options.Accounts.SelectionPath).StartsWithSegments(PagesPath))
            throw new InvalidOperationException($"Accounts.SelectionPath（{options.Accounts.SelectionPath}）不在认证页族托管路径 {PagesPath} 下。");
        var files = new PhysicalFileProvider(Path.GetFullPath(root));
        app.UseStaticFiles(new StaticFileOptions
        {
            RequestPath = PagesPath, FileProvider = files,
            OnPrepareResponse = context => Headers(context.Context.Response,
                context.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? "no-store"
                : context.Context.Request.Path.StartsWithSegments(PagesPath + "/assets") ? "public, max-age=31536000, immutable" : "no-cache")
        });
        return app.Use(async (http, next) =>
        {
            if (http.Request.Path.StartsWithSegments(PagesPath, out var rest) && (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)))
            {
                // 带扩展名却没被静态文件命中的是不存在的资源，不能回入口页（否则脚本请求会拿到 HTML）。
                if (Path.HasExtension(rest.Value)) { Headers(http.Response, "no-store"); http.Response.StatusCode = StatusCodes.Status404NotFound; return; }
                Headers(http.Response, "no-store"); http.Response.ContentType = "text/html; charset=utf-8";
                if (HttpMethods.IsHead(http.Request.Method)) return;
                await http.Response.SendFileAsync(files.GetFileInfo("index.html"), http.RequestAborted).ConfigureAwait(false);
                return;
            }
            await next(http).ConfigureAwait(false);
        });
    }

    private static void Headers(HttpResponse response, string cacheControl)
    {
        response.Headers.CacheControl = cacheControl; response.Headers.XContentTypeOptions = "nosniff";
        response.Headers["Referrer-Policy"] = "no-referrer"; response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";
    }
}
