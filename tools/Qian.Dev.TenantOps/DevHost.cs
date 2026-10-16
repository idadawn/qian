using Kun.EntityFrameworkCore.Extensions;
using Kun.Security.DependencyInjection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Qian.Audit;
using Qian.Authz;
using Qian.Capabilities;
using Qian.Capabilities.AspNetCore;
using Qian.Config;
using Qian.Config.Secrets;
using Qian.Data;
using Qian.Lifecycle;
using Qian.Plugin.Sdk.AspNetCore;
using Qian.Registry;
using Qian.Runtime;
using Qian.Sample.Crm;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;

namespace Qian.Dev.TenantOps;

// 本机租户运营手测宿主（S1-QA-012）。CRM 命令宿主只充当运行时容器（与集成测试同形，监听随机本机端口），
// 产品负责人调用的是另起的运营宿主 TenantOperationsHost。不是产品宿主，不替代 qian dev（S1-RT-013）与 K3s 部署（S1-DEP-002）。
internal static class DevHost
{
    internal static async Task<int> RunAsync(string state, string adminConnection, string url, CancellationToken ct)
    {
        var repository = RepositoryRoot();
        var key = DevKeys.LoadOrCreate(state);
        Console.WriteLine("重建开发库 " + DevDatabase.Name + "（每次启动都从空库开始）……");
        var database = await DevDatabase.ResetAsync(adminConnection, ct).ConfigureAwait(false);
        using var trust = new DevTrustRoot();
        using var platform = new DevPlatform(repository);
        var assembly = new DevRuntimeAssembly();
        await using var business = Business(database, key, trust, assembly);
        await database.CreateSchemasAsync(business.Services, ct).ConfigureAwait(false);
        await business.StartAsync(ct).ConfigureAwait(false);
        await platform.AttachAsync(assembly.Loader, assembly.Providers, trust, ct).ConfigureAwait(false);
        await using var operations = await DevOperations.StartAsync(assembly.Loader, business.Services, platform, key, repository, url, Logging, ct).ConfigureAwait(false);
        Console.WriteLine($"""

            租户运营手测宿主已就绪（本机开发环境，Ctrl+C 停止）
              运营宿主     {operations.Urls.Single()}
              平台超管     sub={DevKeys.Superadmin}（principal_kind=user，无 tenant_id）
              令牌签发方   iss={DevKeys.Issuer}，aud={DevKeys.OperationsAudience}，RS256，kid={key.KeyId}
              业务宿主     {Address(business)}（CRM 运行时容器，手测流程不用）
            下一步：scripts/dev-tenant-ops.sh walkthrough
            """);
        await operations.WaitForShutdownAsync(ct).ConfigureAwait(false);
        return 0;
    }

    // 业务宿主的装配照集成测试 Start(lifecycle: …) 的 ConfigureCapabilities + ConfigureLifecycle；连接全部是受限登录。
    private static WebApplication Business(DevDatabase database, Microsoft.IdentityModel.Tokens.RsaSecurityKey key, DevTrustRoot trust, DevRuntimeAssembly assembly) =>
        CrmCommandApplication.Build([], builder =>
        {
            builder.WebHost.UseUrls("http://127.0.0.1:0"); Logging(builder.Logging);
            builder.Services.AddSingleton<ICrmRuntimeAssembly>(assembly);
            builder.Services.AddKunSecurity();
            builder.Services.AddQianHttpContracts();
            builder.Services.AddQianPluginEndpoints(_ => assembly.Loader.GetContext("registry", ScopeKey.Global));
            builder.Services.AddQianData<CapabilitiesDbContext>((_, options) => options.UseNpgsql(database.Business).UseKunSchema(CapabilitiesDbContext.Schema));
            builder.Services.AddQianDataMigrations(options => options.ResolveConnection = (_, type) =>
                type == typeof(CapabilitiesDbContext) || type == typeof(LifecycleDbContext) ? database.Migrations : null);
            builder.Services.AddQianData<RegistryDbContext>((_, options) => options.UseNpgsql(database.App).UseKunSchema(RegistryDbContext.Schema));
            builder.Services.AddSingleton<ISurfaceReader, SurfaceValidator>();
            builder.Services.AddSingleton<IToolContractHasher, ToolContractHasher>();
            builder.Services.AddQianCapabilities();
            // 契约检查器只在组件升级准入时调用，手测流程不升级组件；本机装了 oasdiff（QIAN_OASDIFF_PATH）就照常接入。
            if (Environment.GetEnvironmentVariable("QIAN_OASDIFF_PATH") is { Length: > 0 } oasdiff && File.Exists(oasdiff))
                builder.Services.AddSingleton<IOpenApiCompatibilityProbe>(_ => new OasDiffOpenApiProbe(oasdiff));
            builder.Services.AddQianData<PlatformAuthzDbContext>((_, options) => options.UseNpgsql(database.AuthzPlatform).UseKunSchema(AuthzDbContext.Schema));
            builder.Services.AddQianData<PlatformAuditDbContext>((_, options) => options.UseNpgsql(database.AuditPlatform).UseKunSchema(AuditDbContext.Schema));
            builder.Services.AddQianData<LifecycleDbContext>((_, options) => options.UseNpgsql(database.Business).UseKunSchema(LifecycleDbContext.Schema));
            builder.Services.AddSingleton<ISecretAccessObserver>(trust);
            builder.Services.AddQianSecrets(new SecretResolverOptions
            {
                MountRoot = trust.SecretRoot,
                Bindings = [new SecretBinding("secretRef://platform/trust/platform", new KubernetesSecretLocation("qian", "trust-platform", "public-key"))],
                Policies = [new SecretAccessPolicy("qian.lifecycle", [new("trust/*", "platform")], [new("trust/*", "platform")])]
            });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Trust:PlatformKeys:0:PublicKey"] = "secretRef://platform/trust/platform", ["Trust:PlatformKeys:0:NotBefore"] = "2026-01-01T00:00:00Z",
                ["ConnectionStrings:Crm"] = database.Business, ["ConnectionStrings:Authz"] = database.App,
                ["ConnectionStrings:Tenancy"] = database.App, ["ConnectionStrings:Audit"] = database.App,
                ["S2S:Issuer"] = DevKeys.Issuer, ["S2S:Audience"] = DevKeys.BusinessAudience, ["S2S:PublicJwks"] = DevKeys.PublicJwks(key),
                ["S2S:ReaderClientId"] = "crm-dev-reader", ["S2S:WriterClientId"] = "crm-dev-writer"
            });
        }, registrar => registrar.RegisterRegistryEvents().RegisterLifecycleEvents(), mediator => mediator.AddQianRegistryEvents().AddQianLifecycleEvents());

    // 只显示警告以上；运营授权拒绝的原因按设计只进结构化日志（响应统一 AUTHZ_DENIED），这里能看到。
    // EF 的命令日志关掉：迁移器首次探测 __EFMigrationsHistory 必然失败一次，属预期；请求里的异常仍由异常处理中间件记录。
    private static void Logging(ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddSimpleConsole(options => { options.SingleLine = true; options.TimestampFormat = "HH:mm:ss "; });
        logging.SetMinimumLevel(LogLevel.Warning);
        logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
    }

    private static string Address(WebApplication app) =>
        app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.SingleOrDefault() ?? "（未知）";

    internal static string RepositoryRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (var root = new DirectoryInfo(start); root is not null; root = root.Parent)
                if (File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) return root.FullName;
        throw new InvalidOperationException("找不到仓库根目录（Qian.slnx）；请在仓库内运行。");
    }
}

// 只借 CRM 宿主公开的装配入口拿到运行时：业务 Provider 原样挂载，不改条目、配置与顺序。
internal sealed class DevRuntimeAssembly : ICrmRuntimeAssembly
{
    private PluginLoader? _loader;
    private IReadOnlyDictionary<string, PluginDefinition>? _providers;
    internal PluginLoader Loader => _loader ?? throw new InvalidOperationException("运行时尚未启动。");
    internal IReadOnlyDictionary<string, PluginDefinition> Providers => _providers ?? throw new InvalidOperationException("运行时尚未启动。");
    public ValueTask<IReadOnlyList<PluginDefinition>> ComposeAsync(PluginLoader runtime, IReadOnlyList<PluginDefinition> businessProviders, CancellationToken cancellationToken)
    {
        _loader = runtime; _providers = businessProviders.ToDictionary(definition => definition.Entry.Id, StringComparer.Ordinal);
        return ValueTask.FromResult(businessProviders);
    }
    public ValueTask ReadyAsync(PluginLoader runtime, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
