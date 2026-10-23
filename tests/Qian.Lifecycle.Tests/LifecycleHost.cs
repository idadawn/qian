using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.Mediator.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Qian.Audit;
using Qian.Config;
using Qian.Config.Apollo;
using Qian.Config.Secrets;
using Qian.Data;
using Qian.Events;
using Qian.Plugin.Sdk;
using Qian.Profiles;
using Qian.Profiles.Abstractions;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;
using Qian.Testing.Containers;

namespace Qian.Lifecycle.Tests;

// 真实平台插件树：Data + Config（宿主配置根 + K8s Secret 挂载解析）+ Registry（PostgreSQL）+ Audit（租户链：批准与租户级操作同事务入链；平台链：平台级操作提交前入链）
// + Lifecycle（包内迁移建表）。
// 信任根与执行器配置走宿主配置根，测试可在运行中改写（模拟 Apollo 发布）；时间由可调的 TimeProvider 给出。
internal sealed class LifecycleHost : IAsyncDisposable
{
    private readonly string _directory;
    private LifecycleHost(string directory, IHost host, PluginLoader runtime, AsyncLocalTenantContext tenant, ManualTime time, MemoryConfigurationProvider settings, SecretObserver secrets, string auditRole)
    {
        _directory = directory; Host = host; Runtime = runtime; Tenant = tenant; Time = time; Settings = settings; Secrets = secrets; AuditRole = auditRole;
    }

    internal IHost Host { get; }
    internal PluginLoader Runtime { get; }
    internal AsyncLocalTenantContext Tenant { get; }
    internal ManualTime Time { get; }
    internal MemoryConfigurationProvider Settings { get; }
    internal SecretObserver Secrets { get; }
    // 平台审计链的专用登录角色（Audit README「受控平台链」）：平台级生命周期操作在提交前经它入平台链（S1-RT-037）。
    internal string AuditRole { get; }
    internal IPluginLifecycleController Lifecycle => Runtime.GetContext("lifecycle", ScopeKey.Global).Lifecycle();
    internal IRegistryService Registry => Runtime.GetContext("registry", ScopeKey.Global).Registry();

    // 预先登记的秘密引用（绑定在宿主启动时固定，文件可以之后再写）：三把平台公钥与 K8s 令牌 / CA。
    internal static readonly string[] TrustKeys = ["k1", "k2", "k3"];
    private static readonly string[] Scopes = ["global", "tenant"];
    private static readonly string[] TrustFields = ["PublicKey", "NotBefore", "NotAfter"];

    // services 供用例登记宿主端口的替身（如权限判定端口）；不替换平台插件树。
    internal static async Task<LifecycleHost> StartAsync(PostgresFixture postgres, object? lifecycleConfig = null,
        Func<string, string, IPluginLifecycleHooks?>? hooks = null, Action<IServiceCollection>? services = null, CancellationToken ct = default)
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "qian-lifecycle-" + Guid.NewGuid().ToString("N"))).FullName;
        var tenant = new AsyncLocalTenantContext();
        var time = new ManualTime(DateTimeOffset.UtcNow);
        var settings = new MemoryConfigurationProvider(new MemoryConfigurationSource());
        var configuration = new ConfigurationRoot([settings]);
        var secrets = new SecretObserver();
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<Kun.Core.MultiTenancy.ITenantContext>(tenant);
        builder.Services.AddSingleton<TimeProvider>(time);
        builder.Services.AddKunEventBus(registry => registry.RegisterRegistryEvents().RegisterLifecycleEvents());
        builder.Services.AddSingleton(ErrorCatalog.CreateDefault());
        builder.Services.AddQianData<RegistryDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(RegistryDbContext.Schema));
        builder.Services.AddQianData<LifecycleDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(LifecycleDbContext.Schema));
        builder.Services.AddQianData<AuditDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(AuditDbContext.Schema));
        // 生命周期上下文随包带迁移：与 --migrate-only 迁移任务同一入口。迁移属主与生产迁移任务一样是受限角色（NOSUPERUSER / NOBYPASSRLS），
        // 只多一项建 schema 的权限；在线连接是受 RLS 约束的应用角色。
        var owner = "qian_lifecycle_owner_" + Guid.NewGuid().ToString("N")[..8];
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await Admin(postgres, $"CREATE ROLE \"{owner}\" LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{password}'; " +
            $"GRANT CONNECT, CREATE ON DATABASE {PostgresFixture.DatabaseName} TO \"{owner}\";", ct);
        var migrations = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Username = owner, Password = password }.ConnectionString;
        builder.Services.AddQianDataMigrations(options => options.ResolveConnection = (_, type) => type == typeof(LifecycleDbContext) ? migrations : null);
        // 平台审计链：独立受限 LOGIN，不与租户审计（qian_app）共用凭据；只在 PlatformAuditDbContext 上绑定，global 审计条目以 chain.platformRole 引用它。
        var auditRole = "qian_audit_platform_" + Guid.NewGuid().ToString("N")[..8];
        var auditPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await Admin(postgres, $"CREATE ROLE \"{auditRole}\" LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{auditPassword}';", ct);
        var auditPlatform = new NpgsqlConnectionStringBuilder(postgres.AppConnectionString) { Username = auditRole, Password = auditPassword }.ConnectionString;
        builder.Services.AddQianData<PlatformAuditDbContext>((_, options) => options.UseNpgsql(auditPlatform).UseKunSchema(AuditDbContext.Schema));
        builder.Services.AddKunMediator(mediator => mediator.AddQianRegistryEvents().AddQianLifecycleEvents());
        builder.Services.AddQianEvents();
        builder.Services.AddSingleton<ISecretAccessObserver>(secrets);
        builder.Services.AddQianSecrets(new SecretResolverOptions
        {
            MountRoot = Path.Combine(directory, "secrets"),
            Bindings = [.. TrustKeys.Select(key => new SecretBinding("secretRef://platform/trust/" + key, new KubernetesSecretLocation("qian", "trust-" + key, "public-key"))),
                new SecretBinding("secretRef://platform/lifecycle/k8s-token", new KubernetesSecretLocation("qian", "lifecycle-k8s", "token")),
                new SecretBinding("secretRef://platform/lifecycle/k8s-ca", new KubernetesSecretLocation("qian", "lifecycle-k8s", "ca"))],
            Policies = [new SecretAccessPolicy("qian.lifecycle", [new("trust/*", "platform"), new("lifecycle/*", "platform")], [new("trust/*", "platform"), new("lifecycle/*", "platform")])]
        });
        builder.Services.AddQianConfiguration(configuration);
        services?.Invoke(builder.Services);
        Directory.CreateDirectory(Path.Combine(directory, "secrets"));
        var host = builder.Build();
        try
        {
            // 数据组件的启动检查拒绝带未应用迁移的库：先迁移，再启动宿主。
            await Prepare(host, postgres, auditRole, ct);
            await host.StartAsync(ct);
            // 装载器的日志器来自宿主：组件记的宿主级告警（如缺审计）才能被用例观察到。
            var runtime = new PluginLoader(host.Services, host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Qian.Runtime.PluginLoader"));
            await MountAsync(runtime, directory, lifecycleConfig, hooks ?? ((_, _) => null), auditRole, ct);
            return new LifecycleHost(directory, host, runtime, tenant, time, settings, secrets, auditRole);
        }
        catch { await host.StopAsync(CancellationToken.None); host.Dispose(); Directory.Delete(directory, true); throw; }
    }

    // 平台公钥：PEM 写进 K8s Secret 挂载，配置节 Trust:PlatformKeys:n 引用它并给出有效期（Apollo qian.common.trust 的形状）。
    internal void Trust(int index, string key, SigningKey signer, DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null)
    {
        WriteSecret("trust-" + key, "public-key", signer.Pem);
        Settings.Set($"Trust:PlatformKeys:{index}:PublicKey", "secretRef://platform/trust/" + key);
        Settings.Set($"Trust:PlatformKeys:{index}:NotBefore", notBefore?.ToString("O"));
        Settings.Set($"Trust:PlatformKeys:{index}:NotAfter", notAfter?.ToString("O"));
    }

    internal void Distrust(int index)
    {
        foreach (var name in TrustFields) Settings.Set($"Trust:PlatformKeys:{index}:{name}", null);
    }

    internal void WriteSecret(string name, string key, string value)
    {
        var target = Directory.CreateDirectory(Path.Combine(_directory, "secrets", "qian", name)).FullName;
        File.WriteAllText(Path.Combine(target, key), value);
    }

    internal static async Task<long> CountAsync(PostgresFixture postgres, string sql, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
    }

    internal static async Task<string?> ScalarAsync(PostgresFixture postgres, string sql, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return (await command.ExecuteScalarAsync(ct))?.ToString();
    }

    // 重新装上审计组件用：与宿主启动时同一条目（带平台链），或只有租户链、没有 chain.platformRole 的条目。
    internal PluginDefinition AuditEntry(bool platformChain = true) => new(new PluginEntry("audit", "qian.audit", "1.0.0", ScopeKey.Global, 0,
        JsonSerializer.SerializeToElement(platformChain ? AuditConfig(AuditRole) : new { })), new AuditPlugin(), [AuditDefinition.Key, CommandPolicies.AuditKey]);

    private static object AuditConfig(string platformRole) => new { chain = new { platformRole } };

    private static async Task Prepare(IHost host, PostgresFixture postgres, string auditRole, CancellationToken ct)
    {
        string registry, audit;
        using (var scope = host.Services.CreateScope())
        {
            registry = scope.ServiceProvider.GetRequiredService<RegistryDbContext>().Database.GenerateCreateScript();
            audit = scope.ServiceProvider.GetRequiredService<AuditDbContext>().Database.GenerateCreateScript();
        }
        await Admin(postgres, "DROP SCHEMA IF EXISTS plugin_qian_registry CASCADE; DROP SCHEMA IF EXISTS plugin_qian_lifecycle CASCADE; DROP SCHEMA IF EXISTS plugin_qian_audit CASCADE;\n" +
            registry + audit + "\nGRANT USAGE ON SCHEMA plugin_qian_registry, plugin_qian_audit TO qian_app; " +
            "GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_qian_registry, plugin_qian_audit TO qian_app;", ct);
        // 审计的同事务入口：函数属主是独立受限的 NOLOGIN 角色，在线应用角色只获函数执行权（与生产迁移同一份 SQL）。
        var writer = "qian_audit_owner_" + Guid.NewGuid().ToString("N")[..8];
        await Admin(postgres, $"CREATE ROLE \"{writer}\" NOLOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION;", ct);
        // 平台链的 RLS 与授权只在租户守卫之后、同一迁移事务里应用（Audit README「受控平台链」）。
        await Admin(postgres, "BEGIN;" + AuditTransactionAccess.CreateMigrationSql(writer, PostgresFixture.AppRoleName) + AuditPlatformAccess.CreateMigrationSql(auditRole) + "COMMIT;", ct);
        var reports = await host.Services.GetRequiredService<IDataMigrator>().MigrateAsync(ct);
        Assert.Equal(["20261015081937_LifecycleStateMachines", "20261021194607_PermissionApprovals"], Assert.Single(reports, report => report.ContextType == typeof(LifecycleDbContext)).Applied);
        await Admin(postgres, "GRANT USAGE ON SCHEMA plugin_qian_lifecycle TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_qian_lifecycle TO qian_app;", ct);
    }

    internal static async Task Admin(PostgresFixture postgres, string sql, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task MountAsync(PluginLoader runtime, string directory, object? lifecycleConfig, Func<string, string, IPluginLifecycleHooks?> hooks, string auditRole, CancellationToken ct)
    {
        var validator = new SurfaceValidator();
        var installed = new List<RegistryComponentBinding>();
        var definitions = new Dictionary<string, PluginDefinition>(StringComparer.Ordinal);
        var options = new ProfileLoadOptions { WorkingDirectory = directory, Profile = "platform", IncludeHomePatch = false };
        Add("data", "qian.data", new DataPlugin(), [DataDefinition.Key]);
        Add("config", "qian.config", new ConfigurationPlugin(), [ConfigDefinition.Key]);
        var registry = new RegistryPlugin(new RegistryRuntimeOptions
        {
            InstalledComponents = installed, Runtime = runtime, Profiles = new ProfileLoader(validator), Profile = options, Surfaces = validator,
            Resolve = entry => definitions[entry.Id] with { Entry = new PluginEntry(entry.Id, entry.Plugin.Split('@')[0], entry.Plugin.Split('@')[1], ScopeKey.Global, entry.Layer, entry.Config) }
        });
        Add("registry", "qian.registry", registry, [RegistryDefinition.Key], new { store = "pg" });
        Add("audit", "qian.audit", new AuditPlugin(), [AuditDefinition.Key, CommandPolicies.AuditKey], AuditConfig(auditRole));
        Add("lifecycle", "qian.lifecycle", new LifecyclePlugin(new LifecycleRuntimeOptions { Hooks = hooks }), [LifecycleDefinition.Key], lifecycleConfig);
        // 组件配置按仓库交付的 config.schema.json 校验（有配置的条目必须带 Schema，audit 条目带 chain.platformRole）。
        foreach (var id in new[] { "registry", "lifecycle", "audit" })
            Write($"plugins/{id}/schemas/config.schema.json", File.ReadAllText(Path.Combine(RepositoryRoot(), $"plugins/qian.{id}/schemas/config.schema.json")));
        Write("profiles/platform/qian.profile.yml", """{"name":"platform","bundles":[],"patch":"./qian.patch.yml","hmr":{"surfaces":true,"code":false},"build":{"aot":false},"hooks":{"allowCommand":false}}""");
        Write("profiles/platform/qian.patch.yml", JsonSerializer.Serialize(definitions.Values.Select(item => new { id = item.Entry.Id, plugin = item.Entry.Plugin + "@" + item.Entry.Version, config = item.Entry.Config })));
        foreach (var definition in definitions.Values)
        {
            var validation = validator.ValidateComponent(Path.Combine(directory, "plugins", definition.Entry.Id),
                new SurfaceValidationOptions { Profile = "platform", ExecutionPlane = "server", CatalogComplete = true });
            Assert.True(validation.IsValid, definition.Entry.Id + ": " + JsonSerializer.Serialize(validation.Diagnostics));
            installed.Add(new RegistryComponentBinding(definition, validation));
        }
        await runtime.MountAsync(definitions.Values, ct);
        foreach (var id in definitions.Keys)
            Assert.True(runtime.GetState(id, ScopeKey.Global) == PluginState.Ready, id + ": " + runtime.GetFailure(id, ScopeKey.Global));
        await runtime.GetContext("registry", ScopeKey.Global).Registry().StartAsync(ct);
        return;

        void Add(string id, string component, IQianPlugin plugin, ServiceKey[] provides, object? config = null)
        {
            var definition = new PluginDefinition(new PluginEntry(id, component, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(config ?? new { })), plugin, provides);
            definitions.Add(id, definition);
            Write($"plugins/{id}/.qian-plugin/plugin.json", JsonSerializer.Serialize(new
            {
                id = component, version = "1.0.0", description = "生命周期集成验收使用实际 Provider 的平台组合。", author = new { name = "Qian" }, license = "Apache-2.0",
                @interface = new { displayName = id, category = "testing" }, inject = plugin.Inject.Select(key => key.Name), provides = provides.Select(key => key.Name),
                policy = new { tier = "system", sandbox = "none", authentication = "ON_INSTALL", required = true }, scopes = Scopes
            }));
        }
        void Write(string path, string content)
        {
            var file = Path.Combine(directory, path); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, content);
        }
    }

    internal static string RepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        return root?.FullName ?? throw new InvalidOperationException("找不到仓库根目录。");
    }

    public async ValueTask DisposeAsync()
    {
        try { await Registry.DisposeAsync(); } catch (ObjectDisposedException) { }
        await Runtime.DisposeAsync();
        await Host.StopAsync(CancellationToken.None); Host.Dispose();
        try { Directory.Delete(_directory, true); } catch (IOException) { }
    }
}

internal sealed class ManualTime(DateTimeOffset now) : TimeProvider
{
    internal DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class SecretObserver : ISecretAccessObserver
{
    internal ConcurrentQueue<SecretAccessEvent> Events { get; } = new();
    public ValueTask RecordAsync(SecretAccessEvent access, CancellationToken cancellationToken = default) { Events.Enqueue(access); return ValueTask.CompletedTask; }
}

// Ed25519 平台签名钥：公钥以 PEM（SubjectPublicKeyInfo）进信任根，指纹 = sha256(SPKI DER)，签名载荷 = D4 摘要的 32 字节原始值。
internal sealed class SigningKey
{
    private readonly Ed25519PrivateKeyParameters _private = new(new SecureRandom());
    internal SigningKey()
    {
        var spki = Convert.FromHexString("302a300506032b6570032100").Concat(_private.GeneratePublicKey().GetEncoded()).ToArray();
        Fingerprint = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(spki));
        Pem = "-----BEGIN PUBLIC KEY-----\n" + Convert.ToBase64String(spki) + "\n-----END PUBLIC KEY-----\n";
    }
    internal string Fingerprint { get; }
    internal string Pem { get; }
    internal ArtifactSignature Sign(string digest)
    {
        var message = Convert.FromHexString(digest["sha256:".Length..]);
        var signer = new Ed25519Signer(); signer.Init(true, _private); signer.BlockUpdate(message, 0, message.Length);
        return new ArtifactSignature(Fingerprint, signer.GenerateSignature());
    }
}

// 待安装的组件文件夹：声明式组件只有清单；service 组件写明 digest 镜像、端口与健康检查路径。
internal static class Artifacts
{
    internal const string Podinfo = "oci://ghcr.io/stefanprodan/podinfo@sha256:862ca45e61b32392f7941a1bdfdbe5ff8b6899070135f1bdca1c287d0057fc94";

    internal static Dictionary<string, ReadOnlyMemory<byte>> Declarative(string id, string version, object? runtime = null, string sandbox = "none")
    {
        var manifest = new Dictionary<string, object?>
        {
            ["$schema"] = "https://qian.dev/schemas/plugin/v2", ["id"] = id, ["version"] = version, ["description"] = "生命周期集成验收使用的组件制品。", ["author"] = new { name = "Acme" },
            ["license"] = "Apache-2.0", ["interface"] = new { displayName = id, category = "testing" }, ["inject"] = Array.Empty<string>(), ["provides"] = Array.Empty<string>(),
            ["policy"] = new { tier = "official", sandbox, authentication = "ON_INSTALL", required = false }, ["scopes"] = new[] { "tenant" }
        };
        if (runtime is not null) manifest["runtime"] = runtime;
        return new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal) { [".qian-plugin/plugin.json"] = JsonSerializer.SerializeToUtf8Bytes(manifest) };
    }

    // S1 的 service 组件必须是受信任工作负载（official / system + sandbox: workload）。
    internal static Dictionary<string, ReadOnlyMemory<byte>> Service(string id, string version, int port = 9898, string health = "/healthz") =>
        Declarative(id, version, new { form = "service", image = Podinfo, service = new { port, health } }, "workload");

    internal static string Digest(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> files)
    {
        var inventory = string.Concat(files.OrderBy(file => file.Key, StringComparer.Ordinal).Select(file =>
            file.Key + "\t" + file.Value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\t" + Convert.ToHexStringLower(SHA256.HashData(file.Value.Span)) + "\n"));
        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(inventory)));
    }

    internal static PluginArtifact Signed(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> files, SigningKey key)
    {
        var digest = Digest(files);
        return new PluginArtifact(files, digest, key.Sign(digest));
    }
}
