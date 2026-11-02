using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Kun.EntityFrameworkCore.Extensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.FileProviders;
using OpenIddict.Abstractions;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Identity.Server;
using Qian.Runtime;
using Qian.Tenancy;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Qian.Dev.TenantOps;

// 运营手测宿主上的真实 qian.identity（S1-TEN-010）：管理员维护的账号查找、新建、管理员成员与重置密码都经它，
// 开通编排第 05 步也由它建管理员成员。照集成测试 CustomerCommandTests.Identity 的装配，只开账号 HTTP 接口与目录接口（统一登录流水线），
// 不托管认证页族、不挂上游连接器、不签发外壳令牌——那些在外壳联调环境（tools/Qian.Dev.Shell 的 DevIdentity）。
internal sealed class DevTenantIdentity(Uri issuer) : IDisposable
{
    /// <summary>登录接口要求的已登记客户端；手测与 E2E 只用账号会话，不走授权码。</summary>
    internal const string ClientId = "qian-tenant-ops";
    private const string Resource = "https://api.qian.local";
    private readonly X509Certificate2 _certificate = Certificate();
    private readonly string _secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    internal Uri Issuer => issuer;

    internal void Configure(WebApplicationBuilder builder, DevDatabase database)
    {
        builder.Services.AddQianData<IdentityDbContext>((_, options) => options.UseNpgsql(database.App).UseKunSchema(IdentityDbContext.Schema));
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthentication().AddCookie("qian.identity.signin");
        builder.Services.AddHttpClient();
        var client = new OpenIddictApplicationDescriptor { ClientId = ClientId, ClientSecret = _secret, ClientType = ClientTypes.Confidential, ConsentType = ConsentTypes.Implicit, DisplayName = "租户运营手测（本机）" };
        client.RedirectUris.Add(new Uri(issuer, "callback"));
        client.Permissions.UnionWith([Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.GrantTypes.AuthorizationCode, Permissions.ResponseTypes.Code]);
        builder.Services.AddSingleton<IStartupFilter>(new IdentityBranchFilter());
        builder.Services.AddQianIdentityServer(new IdentityServerOptions
        {
            Issuer = issuer, AllowLoopbackHttp = true, SigningCertificates = [_certificate], EncryptionCertificates = [_certificate],
            Clients = [new() { Application = client, Resources = [Resource] }]
        }, new DevelopmentEnvironment());
    }

    // 身份库的建表脚本（与生产同一套迁移）与应用登录的授权。
    internal static string SchemaSql(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IdentityDbContext>().GetService<IMigrator>().GenerateScript() +
            $"GRANT USAGE ON SCHEMA {IdentityDbContext.Schema} TO {DevDatabase.AppRole}; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {IdentityDbContext.Schema} TO {DevDatabase.AppRole};";
    }

    // 内置账号模型连同成员开户与用户计数端口一并提供：开户第 05 步经 MembershipKey 建管理员成员，配额用量经 UserCounterKey 读真实人数。
    internal async Task MountAsync(PluginLoader loader, CancellationToken ct)
    {
        await loader.MountAsync([new PluginDefinition(new PluginEntry("identity", "qian.identity.server", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { issuer = issuer.AbsoluteUri })),
            new IdentityServerPlugin(), [IdentityDefinition.Key, IdentityDefinition.AccountsKey, IdentityDefinition.SignInKey, IdentityDefinition.FactorsKey, TenantProvisioning.MembershipKey, TenantSettings.UserCounterKey])], ct).ConfigureAwait(false);
        DevPlatform.EnsureReady(loader, "identity");
    }

    public void Dispose() => _certificate.Dispose();

    private static X509Certificate2 Certificate()
    {
        using var key = RSA.Create(2048);
        return new CertificateRequest("CN=Qian dev tenant ops identity", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(7));
    }

    // 协议库只在开发环境允许回环 HTTP 发行方；宿主本身不改环境，只在注册时声明这一点。
    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "qian.dev.tenant-ops";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    // 同机混合部署：身份的账号接口在 /api 之下，而业务宿主对 /api 一律要求已认证的租户；身份请求走自己的分支，不经过业务管线。
    private sealed class IdentityBranchFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.MapWhen(http => http.Request.Path.StartsWithSegments("/api/identity") || http.Request.Path.StartsWithSegments("/api/directory") ||
                http.Request.Path.StartsWithSegments("/connect") || http.Request.Path.StartsWithSegments("/.well-known"), branch =>
            {
                branch.UseQianIdentityServer(); branch.UseRouting(); branch.UseAuthentication();
                branch.UseEndpoints(endpoints => { endpoints.MapQianIdentityServer(); endpoints.MapQianIdentityAccounts(); });
            });
            next(app);
        };
    }
}
