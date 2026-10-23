using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Kun.EntityFrameworkCore.Extensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.FileProviders;
using OpenIddict.Abstractions;
using Qian.Data;
using Qian.Dev.TenantOps;
using Qian.Identity.Abstractions;
using Qian.Identity.Server;
using Qian.Runtime;
using Qian.Tenancy;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Qian.Dev.Shell;

// 真实 qian.identity 挂进 CRM 参考宿主（照集成测试 CustomerCommandTests.Identity 的 ConfigureIdentity / MountIdentity）：
// 内置账号模型 + OpenIddict 协议面 + 账号 HTTP 接口，身份请求走自己的分支，不经过业务管线；同一分支同源托管认证页族静态制品（/login）。
internal sealed class DevIdentity(Uri issuer, Uri callback, string uiDist, string clientSecret) : IDisposable
{
    internal const string ClientId = "qian-shell";
    private readonly X509Certificate2 _certificate = Certificate();

    internal void Configure(WebApplicationBuilder builder, DevDatabase database)
    {
        builder.Services.AddQianData<IdentityDbContext>((_, options) => options.UseNpgsql(database.App).UseKunSchema(IdentityDbContext.Schema));
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        // 账号 Cookie scheme：未登录的授权请求挑战到同源认证页族 /login?ReturnUrl=…（页面登录后回到授权请求签发授权码）。
        builder.Services.AddAuthentication().AddCookie("qian.identity.signin", cookie => cookie.LoginPath = "/login");
        builder.Services.AddHttpClient();
        var shell = new OpenIddictApplicationDescriptor { ClientId = ClientId, ClientSecret = clientSecret, ClientType = ClientTypes.Confidential, ConsentType = ConsentTypes.Implicit, DisplayName = "Qian 外壳（本机联调）" };
        shell.RedirectUris.Add(callback);
        shell.Permissions.UnionWith([Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.Endpoints.EndSession, Permissions.Endpoints.Revocation,
            Permissions.GrantTypes.AuthorizationCode, Permissions.GrantTypes.RefreshToken, Permissions.ResponseTypes.Code, Permissions.Prefixes.Scope + "qian.api"]);
        builder.Services.AddSingleton<IStartupFilter>(new IdentityBranchFilter(uiDist));
        builder.Services.AddQianIdentityServer(new IdentityServerOptions
        {
            Issuer = issuer, AllowLoopbackHttp = true, SigningCertificates = [_certificate], EncryptionCertificates = [_certificate],
            Clients = [new() { Application = shell, Resources = [DevGateway.Resource] }]
        }, new DevelopmentEnvironment());
    }

    // 身份库的建表脚本（与生产同一套迁移）与应用登录的授权。
    internal static string SchemaSql(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IdentityDbContext>().GetService<IMigrator>().GenerateScript() +
            $"GRANT USAGE ON SCHEMA {IdentityDbContext.Schema} TO {DevDatabase.AppRole}; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {IdentityDbContext.Schema} TO {DevDatabase.AppRole};";
    }

    // 挂载身份组件：内置账号模型连同成员开户与用户计数端口一并提供（开通编排第 05 步经 MembershipKey 建管理员成员）。
    internal async Task MountAsync(PluginLoader loader, CancellationToken ct)
    {
        await loader.MountAsync([new PluginDefinition(new PluginEntry("identity", "qian.identity.server", "1.0.0", ScopeKey.Global, 0, System.Text.Json.JsonSerializer.SerializeToElement(new { issuer = issuer.AbsoluteUri })),
            new IdentityServerPlugin(), [IdentityDefinition.Key, IdentityDefinition.AccountsKey, IdentityDefinition.SignInKey, IdentityDefinition.FactorsKey, TenantProvisioning.MembershipKey, TenantSettings.UserCounterKey])], ct).ConfigureAwait(false);
        DevPlatform.EnsureReady(loader, "identity");
    }

    public void Dispose() => _certificate.Dispose();

    private static X509Certificate2 Certificate()
    {
        using var key = RSA.Create(2048);
        return new CertificateRequest("CN=Qian dev identity", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(7));
    }

    // 协议库只在开发环境允许回环 HTTP 发行方；宿主本身不改环境，只在注册时声明这一点。
    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "qian.dev.shell";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    // 同机混合部署：身份的账号接口在 /api 之下，而业务宿主对 /api 一律要求已认证的租户；身份请求与认证页族走自己的分支。
    private sealed class IdentityBranchFilter(string uiDist) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.MapWhen(http => http.Request.Path.StartsWithSegments("/api/identity") || http.Request.Path.StartsWithSegments("/connect") ||
                http.Request.Path.StartsWithSegments("/.well-known") || http.Request.Path.StartsWithSegments("/login"), branch =>
            {
                branch.UseQianIdentityServer();
                var files = new PhysicalFileProvider(uiDist);
                // 认证页族是 Vite 构建产物（base /login/）：带哈希的资源长缓存，入口页不缓存；页面只许同源嵌入自己（不许被 iframe）。
                branch.UseStaticFiles(new StaticFileOptions
                {
                    RequestPath = "/login", FileProvider = files,
                    OnPrepareResponse = context => Headers(context.Context.Response, context.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? "no-store" : "public, max-age=31536000, immutable")
                });
                branch.Use(async (http, next) =>
                {
                    if (http.Request.Path.StartsWithSegments("/login", out var rest) && (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)))
                    {
                        if (Path.HasExtension(rest.Value)) { http.Response.StatusCode = StatusCodes.Status404NotFound; return; }
                        Headers(http.Response, "no-store"); http.Response.ContentType = "text/html; charset=utf-8";
                        await http.Response.SendFileAsync(files.GetFileInfo("index.html"), http.RequestAborted).ConfigureAwait(false);
                        return;
                    }
                    await next(http).ConfigureAwait(false);
                });
                branch.UseRouting(); branch.UseAuthentication();
                branch.UseEndpoints(endpoints => { endpoints.MapQianIdentityServer(); endpoints.MapQianIdentityAccounts(); });
            });
            next(app);
        };

        private static void Headers(HttpResponse response, string cacheControl)
        {
            response.Headers.CacheControl = cacheControl; response.Headers.XContentTypeOptions = "nosniff";
            response.Headers["Referrer-Policy"] = "no-referrer"; response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";
        }
    }
}
