using System.Net;
using Kun.Core.MultiTenancy;
using Qian.Dev.TenantOps;
using Qian.Identity.Abstractions;
using Qian.Identity.Server;
using Microsoft.IdentityModel.Tokens;
using Qian.Gateway;
using Qian.Runtime;
using Qian.Sample.Crm;

namespace Qian.Dev.Shell;

/// <summary>启动参数：状态目录、管理员连接、身份 / 业务宿主地址（即 OIDC 发行方）、网关 BFF 地址，以及可选的上游 Dex（issuer 与客户端密钥文件）。</summary>
internal sealed record DevShellSettings(string State, string Admin, string Urls, string Gateway, string? Dex = null, string? DexSecret = null, int RedisPort = 55434);

// 外壳认证联调环境（S1-YUE-002 / S1-YUE-012）。装配照搬租户运营手测宿主（S1-QA-012）与集成测试：
//   1. 重建开发库，CRM 参考命令宿主充当运行时容器，并在同一宿主上挂真实 qian.identity（内置账号模型，账号 Cookie 的登录页指向同源静态认证页族 /login），
//      再挂通用 OIDC 连接器 dex 指向脚本拉起的 CNCF Dex（SSO 跳转回调的真实上游）；
//   2. 平台插件树（Data / Tenancy / Authz / Audit / Config / Registry / Capabilities / Lifecycle）加参考应用制品，经超管安装激活；
//   3. 按种子文件建自然人、经真实开通编排建租户（成员与 tenant.owner 授权由编排完成），种子再把参考应用挂进租户 scope（丙方不挂：验收 2 的「未挂载」形态），补其余成员与 tenant.user 授权；
//   4. 起面向用户的平台表面宿主（参考宿主 PlatformSurfaceHost：能力目录 / 导航 / 外壳目录与有效权限，授权 PEP 按生产模式执行身份修订下限）；
//   5. 起 Identity.Client / Qian.Gateway BFF 宿主（HTTPS 自签名）：产品扩展静态托管外壳（apps/qian-shell）与参考应用的远程制品（samples/qian-sample-crm-ui），
//      /app-config.json 取自状态目录，bootstrap 聚合端点，/api 经 S2S 按受众分簇转发到业务宿主与平台表面宿主。
// 不是产品宿主，不替代 qian dev（S1-RT-013）与 K3s 部署（S1-DEP-002）；浏览器只持有 HttpOnly Cookie，不接触任何令牌。
internal static class DevShellHost
{
    internal static async Task<int> RunAsync(DevShellSettings settings, CancellationToken ct)
    {
        var repository = DevHost.RepositoryRoot();
        var issuer = Loopback(settings.Urls, "--urls", https: false);
        var gateway = Loopback(settings.Gateway, "--gateway", https: true);
        var shellDist = Dist(repository, "apps/qian-shell/dist/host", "index.html", "qian-shell");
        var identityDist = Dist(repository, "packages/qian-identity-ui/dist", "index.html", "@qian/identity-ui");
        var crmRemote = Dist(repository, "samples/qian-sample-crm-ui/dist/remote", "mf-manifest.json", "@qian/sample-crm-ui");
        var seed = DevSeed.Load(Path.Combine(repository, "tools", "Qian.Dev.Shell", "seed", "accounts.json"));
        Directory.CreateDirectory(settings.State);
        Console.WriteLine("重建开发库 " + DevDatabase.Name + "（每次启动都从空库开始）……");
        var database = await DevDatabase.ResetAsync(settings.Admin, ct).ConfigureAwait(false);
        using var trust = new DevTrustRoot();
        using var platform = new DevPlatform(repository) { IncludeReferenceApplication = true };
        var assembly = new DevRuntimeAssembly();
        using var keys = DevGatewayKeys.Create(settings.State);
        var dex = settings.Dex is null ? null : new DevDexConnector(Loopback(settings.Dex, "--dex", https: false, allowPath: true), settings.DexSecret!);
        using var identity = new DevIdentity(issuer, new Uri(gateway, "auth/callback"), identityDist, keys.ClientSecret, dex);
        await using var business = DevHost.Business(database, trust, assembly, new BusinessS2s(DevGateway.Issuer, DevGateway.Cluster, keys.PublicJwks), issuer.AbsoluteUri,
            builder => identity.Configure(builder, database), registrar => registrar.RegisterIdentityEvents(), mediator => mediator.AddQianIdentityEvents());
        // 身份库与生产同一套迁移，先于迁移器建表（迁移器只对 Capabilities / Lifecycle 用属主连接）；在线连接仍是受限应用登录。
        await database.ExecuteAdminAsync(DevIdentity.SchemaSql(business.Services), ct).ConfigureAwait(false);
        await database.CreateSchemasAsync(business.Services, ct).ConfigureAwait(false);
        await business.StartAsync(ct).ConfigureAwait(false);
        await platform.AttachAsync(assembly.Loader, assembly.Providers, trust, ct, business.Services).ConfigureAwait(false);
        await identity.MountAsync(assembly.Loader, ct).ConfigureAwait(false);
        await DevOperations.PrepareAsync(assembly.Loader, business.Services, platform, repository, ct).ConfigureAwait(false);
        var tenants = await DevSeed.ApplyAsync(seed, assembly.Loader, business.Services, ct).ConfigureAwait(false);
        // 开发路由 /__dev/identity/bump 经真实身份端口递增当前成员的 identity_revision（授权变更后身份侧就是这样做的），网关随即经平台表面的 PEP 把修订下限抬上去。
        var accounts = assembly.Loader.GetContext("identity", ScopeKey.Global).IdentityAccounts();
        var tenantContext = business.Services.GetRequiredService<ITenantContext>();
        async Task<long> Bump(Principal principal, CancellationToken token)
        {
            using var scope = tenantContext.BeginScope(principal.TenantId!);
            return await accounts.BumpIdentityRevisionAsync(Guid.ParseExact(principal.Subject, "D"), "dev.identity.bump", token).ConfigureAwait(false);
        }
        // 平台表面宿主只认网关签发的 S2S（RS256、issuer、受众 qian-platform、有效期，不留时钟偏差）。
        await using var platformApi = PlatformSurfaceHost.Build(id => assembly.Loader.GetContext(id, ScopeKey.Global), new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = DevGateway.Issuer, ValidateAudience = true, ValidAudience = PlatformSurfaceHost.Audience, ValidateLifetime = true,
            ValidateIssuerSigningKey = true, IssuerSigningKey = keys.PublicKey, ValidAlgorithms = [SecurityAlgorithms.RsaSha256], RequireSignedTokens = true,
            RequireExpirationTime = true, ClockSkew = TimeSpan.Zero
        }, builder => { builder.WebHost.UseUrls("http://127.0.0.1:0"); DevHost.Logging(builder.Logging); });
        await platformApi.StartAsync(ct).ConfigureAwait(false);
        // 运行时配置：K8s 里是 ConfigMap 挂成同源的 /app-config.json；这里取状态目录里的文件（网关每次现读，改完刷新即生效）。首次启动写缺省值，之后不覆盖。
        var appConfig = Path.Combine(settings.State, "app-config.json");
        if (!File.Exists(appConfig)) await File.WriteAllTextAsync(appConfig, DevGateway.DefaultAppConfig, ct).ConfigureAwait(false);
        var shell = new ShellStaticOptions { Root = shellDist, ConfigFile = appConfig, Remotes = new Dictionary<string, string>(StringComparer.Ordinal) { ["qian.sample.crm"] = crmRemote } };
        await using var bff = await DevGateway.StartAsync(gateway, keys, issuer, DevHost.Address(business), DevHost.Address(platformApi), shell, Bump, settings.RedisPort, ct).ConfigureAwait(false);
        Console.WriteLine($"""

            外壳认证联调环境已就绪（本机开发环境，Ctrl+C 停止）
              外壳 / 网关 BFF   {gateway}（自签名证书 {keys.TlsCertificatePath}）
              身份 / 业务宿主    {issuer}（OIDC 发行方；认证页族 /login，账号接口 /api/identity）
              平台表面宿主      {DevHost.Address(platformApi)}（能力目录 / 导航 / 外壳目录与有效权限，受众 {PlatformSurfaceHost.Audience}；网关按前缀分簇转发）
              运行时配置        {appConfig}（改 apiBase 后刷新页面即生效，不重编译）
              种子账号          tools/Qian.Dev.Shell/seed/accounts.json（{string.Join("、", seed.Persons.Select(person => person.Username))}）
              上游 Dex          {(dex is null ? "未配置（不登记连接器）" : dex.Issuer.AbsoluteUri + "（通用 OIDC 连接器 dex；用户见 seed/dex.json）")}
              租户              {string.Join("  ", tenants.Select(tenant => tenant.Code + "=" + tenant.Id.ToString("D")))}
            下一步：浏览器打开外壳地址，或 scripts/dev-shell.sh e2e
            """);
        await bff.App.WaitForShutdownAsync(ct).ConfigureAwait(false);
        return 0;
    }

    // 两个宿主与上游 Dex 都只接受回环地址：联调环境放行了明文 HTTP 发行方与开发路由，不得暴露到本机之外。
    private static Uri Loopback(string value, string option, bool https, bool allowPath = false)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != (https ? "https" : "http") || (!allowPath && uri.PathAndQuery != "/") || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException($"{option} 必须是{(allowPath ? "" : "不含路径的 ")}{(https ? "https" : "http")} 地址。");
        if (uri.Host != "localhost" && !(IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address)))
            throw new ArgumentException($"{option} 只允许回环地址（localhost、127.0.0.1、::1）。");
        return uri;
    }

    private static string Dist(string repository, string directory, string marker, string filter)
    {
        var dist = Path.Combine(repository, directory.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(Path.Combine(dist, marker))) throw new InvalidOperationException($"缺少前端构建产物 {directory}/{marker}；先运行：pnpm --filter {filter} build");
        return dist;
    }
}
