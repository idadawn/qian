using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace Qian.Tenancy.Signup;

/// <summary>申请人公开页（注册并申请开通 <c>/signup</c>、申请进度 <c>/signup/status</c>）与申请人接口同源的静态托管（规格 §4、§8）。</summary>
public static class TenancySignupPagesHosting
{
    /// <summary>申请人公开页的托管路径（构建产物的 Vite base 为 <c>/signup/</c>）。</summary>
    public const string PagesPath = "/signup";

    /// <summary>
    /// 放在 UseRouting 之前：同源托管申请人公开页。<c>/signup</c> 下的资源由静态文件提供（<c>/signup/assets/</c> 带哈希长缓存），
    /// 不带扩展名的页面路由（<c>/signup</c>、<c>/signup/status</c> 等）一律回入口 index.html；带扩展名却不存在的资源 404，不回入口页。
    /// HTML 不缓存，全部响应带 nosniff、<c>Referrer-Policy: no-referrer</c> 与 <c>Content-Security-Policy: frame-ancestors 'none'</c>（页面不许被任何来源嵌入）。
    /// </summary>
    public static IApplicationBuilder UseQianTenancySignupPages(this IApplicationBuilder app, string root)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (string.IsNullOrWhiteSpace(root) || !File.Exists(Path.Combine(root, "index.html"))) throw new ArgumentException("申请人公开页目录必须存在且含 index.html。", nameof(root));
        var files = new PhysicalFileProvider(Path.GetFullPath(root));
        app.UseStaticFiles(new StaticFileOptions
        {
            RequestPath = PagesPath, FileProvider = files,
            OnPrepareResponse = context => Headers(context.Context.Response,
                context.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? "no-store"
                : context.Context.Request.Path.StartsWithSegments(PagesPath + "/assets", StringComparison.Ordinal) ? "public, max-age=31536000, immutable" : "no-cache")
        });
        return app.Use(async (http, next) =>
        {
            if (http.Request.Path.StartsWithSegments(PagesPath, StringComparison.Ordinal, out var rest) && (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)))
            {
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
