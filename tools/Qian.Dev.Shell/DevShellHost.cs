using System.Net;
using Qian.Dev.TenantOps;
using Qian.Identity.Abstractions;
using Qian.Identity.Server;

namespace Qian.Dev.Shell;

/// <summary>启动参数：状态目录、管理员连接、身份 / 业务宿主地址（即 OIDC 发行方）与网关 BFF 地址。</summary>
internal sealed record DevShellSettings(string State, string Admin, string Urls, string Gateway);

// 外壳认证联调环境（S1-YUE-002）。装配照搬租户运营手测宿主（S1-QA-012）与集成测试：
//   1. 重建开发库，CRM 参考命令宿主充当运行时容器，并在同一宿主上挂真实 qian.identity（内置账号模型，账号 Cookie 的登录页指向同源静态认证页族 /login）；
//   2. 平台插件树（Data / Tenancy / Authz / Audit / Config / Registry / Capabilities / Lifecycle）加参考应用制品，经超管安装激活；
//   3. 按种子文件建自然人、经真实开通编排建租户（成员、tenant.owner 授权与参考应用进租户 scope 都由编排完成）、补第二租户的成员与 tenant.user 授权；
//   4. 起 Identity.Client / Qian.Gateway BFF 宿主（HTTPS 自签名）：静态托管外壳、/app-conf.js 取自状态目录、/api 经 S2S 转发到业务宿主。
// 不是产品宿主，不替代 qian dev（S1-RT-013）与 K3s 部署（S1-DEP-002）；浏览器只持有 HttpOnly Cookie，不接触任何令牌。
internal static class DevShellHost
{
    internal static async Task<int> RunAsync(DevShellSettings settings, CancellationToken ct)
    {
        var repository = DevHost.RepositoryRoot();
        var issuer = Loopback(settings.Urls, "--urls", https: false);
        var gateway = Loopback(settings.Gateway, "--gateway", https: true);
        var shellDist = Dist(repository, "apps/qian-shell-auth", "qian-shell-auth");
        var identityDist = Dist(repository, "packages/qian-identity-ui", "@qian/identity-ui");
        var seed = DevSeed.Load(Path.Combine(repository, "tools", "Qian.Dev.Shell", "seed", "accounts.json"));
        Directory.CreateDirectory(settings.State);
        Console.WriteLine("重建开发库 " + DevDatabase.Name + "（每次启动都从空库开始）……");
        var database = await DevDatabase.ResetAsync(settings.Admin, ct).ConfigureAwait(false);
        using var trust = new DevTrustRoot();
        using var platform = new DevPlatform(repository) { IncludeReferenceApplication = true };
        var assembly = new DevRuntimeAssembly();
        using var keys = DevGatewayKeys.Create(settings.State);
        using var identity = new DevIdentity(issuer, new Uri(gateway, "auth/callback"), identityDist, keys.ClientSecret);
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
        await using var platformApi = await DevPlatformApi.StartAsync(assembly.Loader, business.Services, DevGateway.Issuer, keys.PublicKey, ct).ConfigureAwait(false);
        await using var bff = await DevGateway.StartAsync(gateway, keys, issuer, DevHost.Address(business), DevHost.Address(platformApi), shellDist, settings.State, ct).ConfigureAwait(false);
        Console.WriteLine($"""

            外壳认证联调环境已就绪（本机开发环境，Ctrl+C 停止）
              外壳 / 网关 BFF   {gateway}（自签名证书 {keys.TlsCertificatePath}）
              身份 / 业务宿主    {issuer}（OIDC 发行方；认证页族 /login，账号接口 /api/identity）
              平台表面宿主      {DevHost.Address(platformApi)}（能力导航目录与有效权限，受众 {DevPlatformApi.Audience}；网关按前缀分簇转发）
              运行时配置        {Path.Combine(settings.State, "app-conf.js")}（改 apiBase 后刷新页面即生效，不重编译）
              种子账号          tools/Qian.Dev.Shell/seed/accounts.json（{string.Join("、", seed.Persons.Select(person => person.Username))}）
              租户              {string.Join("  ", tenants.Select(tenant => tenant.Code + "=" + tenant.Id.ToString("D")))}
            下一步：浏览器打开外壳地址，或 scripts/dev-shell.sh e2e
            """);
        await bff.App.WaitForShutdownAsync(ct).ConfigureAwait(false);
        return 0;
    }

    // 两个宿主都只接受回环地址：联调环境放行了明文 HTTP 发行方与开发路由，不得暴露到本机之外。
    private static Uri Loopback(string value, string option, bool https)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != (https ? "https" : "http") || uri.PathAndQuery != "/" || uri.Fragment.Length != 0)
            throw new ArgumentException($"{option} 必须是不含路径的 {(https ? "https" : "http")} 地址。");
        if (uri.Host != "localhost" && !(IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address)))
            throw new ArgumentException($"{option} 只允许回环地址（localhost、127.0.0.1、::1）。");
        return uri;
    }

    private static string Dist(string repository, string package, string filter)
    {
        var dist = Path.Combine(repository, package.Replace('/', Path.DirectorySeparatorChar), "dist");
        if (!File.Exists(Path.Combine(dist, "index.html"))) throw new InvalidOperationException($"缺少前端构建产物 {package}/dist；先运行：pnpm --filter {filter} build");
        return dist;
    }
}
