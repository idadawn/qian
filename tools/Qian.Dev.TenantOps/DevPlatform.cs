using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Qian.Audit;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Capabilities;
using Qian.Config;
using Qian.Config.Apollo;
using Qian.Data;
using Qian.Lifecycle;
using Qian.Plugin.Sdk;
using Qian.Profiles;
using Qian.Profiles.Abstractions;
using Qian.Registry;
using Qian.Runtime;
using Qian.Sample.Crm;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;
using Qian.Tenancy;

namespace Qian.Dev.TenantOps;

// 平台插件树，照集成测试 LifecycleAcceptanceTests 的 AttachLifecycle 装配：Data / Tenancy / Authz / Audit / Config / Registry /
// Capabilities / Lifecycle 都是实际 Provider，组件文件夹写进临时 platform profile，制品摘要经本次启动的平台钥签名后交给 Lifecycle 安装。
// 与测试不同的只有两处：不预装参考应用的多个版本（手测不涉及 CRM 升级），也不给示例租户授予诊断权限。
internal sealed class DevPlatform(string repository) : IDisposable
{
    private static readonly string[] CatalogScopes = ["global", "tenant"];
    private static readonly string[] GlobalSettingsScopes = ["global"];
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("qian-dev-tenant-ops-");
    internal Dictionary<string, PluginArtifact> SystemArtifacts { get; } = new(StringComparer.Ordinal);

    internal async Task AttachAsync(PluginLoader loader, IReadOnlyDictionary<string, PluginDefinition> business, DevTrustRoot trust, CancellationToken ct)
    {
        var validator = new SurfaceValidator(); var installed = new List<RegistryComponentBinding>();
        var options = new ProfileLoadOptions { WorkingDirectory = _directory.FullName, Profile = "platform", IncludeHomePatch = false };
        var definitions = new Dictionary<string, PluginDefinition>(StringComparer.Ordinal);
        Add("data", "qian.data", new DataPlugin(), [DataDefinition.Key]);
        // Tenancy 配置沿用业务宿主实际挂载的条目（resolver 与 initializationCapabilities），不另造一份。
        Add("tenancy", "qian.tenancy", new TenancyPlugin(), [TenancyDefinition.Key, CommandPolicies.TenancyKey], business["tenancy"].Entry.Config);
        Add("authz", "qian.authz", new AuthzPlugin([CrmPermissions.Compile()]), [CommandPolicies.AuthorizationKey, AuthzDefinition.Key]);
        Add("audit", "qian.audit", new AuditPlugin(), [AuditDefinition.Key, CommandPolicies.AuditKey]);
        Add("config", "qian.config", new ConfigurationPlugin(), [ConfigDefinition.Key]);
        var registry = new RegistryPlugin(new RegistryRuntimeOptions
        {
            InstalledComponents = installed, Runtime = loader, Profiles = new ProfileLoader(validator), Profile = options, Surfaces = validator,
            Resolve = entry =>
            {
                var scope = entry.Scope == "global" ? ScopeKey.Global : new ScopeKey(entry.Scope.Split(':', 2)[0], entry.Scope.Split(':', 2)[1]);
                var reference = entry.Plugin.Split('@');
                return definitions[entry.Id] with { Entry = new PluginEntry(entry.Id, reference[0], reference[1], scope, entry.Layer, entry.Config) };
            }
        });
        Add("registry", "qian.registry", registry, [RegistryDefinition.Key], new { store = "pg" });
        Add("capabilities", "qian.capabilities", new CapabilitiesPlugin(), [CapabilitiesDefinition.Key, CapabilitiesDefinition.ContractsKey, CapabilitiesDefinition.NavigationKey]);
        Add("lifecycle", "qian.lifecycle", new LifecyclePlugin(new LifecycleRuntimeOptions { Hooks = (id, _) => id == "qian.sample.crm" ? new CrmPlugin() : null }), [LifecycleDefinition.Key]);
        foreach (var id in new[] { "registry", "capabilities", "lifecycle", "audit", "tenancy" })
            Write($"plugins/{id}/schemas/config.schema.json", File.ReadAllText(Path.Combine(repository, $"plugins/qian.{id}/schemas/config.schema.json")));
        Write("profiles/platform/qian.profile.yml", """{"name":"platform","bundles":[],"patch":"./qian.patch.yml","hmr":{"surfaces":true,"code":false},"build":{"aot":false},"hooks":{"allowCommand":false}}""");
        Write("profiles/platform/qian.patch.yml", JsonSerializer.Serialize(definitions.Values.Select(item => new { id = item.Entry.Id, plugin = item.Entry.Plugin + "@" + item.Entry.Version, config = item.Entry.Config })));
        var surface = new SurfaceValidationOptions { Profile = "platform", ExecutionPlane = "server", CatalogComplete = true };
        foreach (var definition in definitions.Values)
        {
            var folder = Path.Combine(_directory.FullName, "plugins", definition.Entry.Id);
            var validation = validator.ValidateComponent(folder, surface);
            if (!validation.IsValid) throw new InvalidOperationException(definition.Entry.Id + " 组件文件夹不合格：" + JsonSerializer.Serialize(validation.Diagnostics));
            var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).ToDictionary(
                file => Path.GetRelativePath(folder, file).Replace(Path.DirectorySeparatorChar, '/'), file => new ReadOnlyMemory<byte>(File.ReadAllBytes(file)), StringComparer.Ordinal);
            var digest = ArtifactDigest(files);
            installed.Add(RegistryComponentBinding.FromArtifact(definition, files, digest, validator, surface));
            SystemArtifacts[definition.Entry.Plugin] = new PluginArtifact(files, digest, trust.Sign(digest));
        }
        // 参考应用只经 Lifecycle 进租户 scope；手测不开通它，撤下 CRM 宿主启动时挂的全局条目。
        await loader.UnmountAsync("crm", ScopeKey.Global, ct).ConfigureAwait(false);
        await loader.MountAsync([definitions["config"], definitions["registry"], definitions["capabilities"], definitions["lifecycle"]], ct).ConfigureAwait(false);
        foreach (var id in new[] { "config", "registry", "capabilities", "lifecycle" }) EnsureReady(loader, id);
        await loader.GetContext("registry", ScopeKey.Global).Registry().StartAsync(ct).ConfigureAwait(false);

        void Add(string id, string component, IQianPlugin plugin, ServiceKey[] provides, object? config = null)
        {
            var definition = new PluginDefinition(new PluginEntry(id, component, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(config ?? new { })), plugin, provides);
            definitions.Add(id, definition);
            Write($"plugins/{id}/.qian-plugin/plugin.json", JsonSerializer.Serialize(new
            {
                id = component, version = "1.0.0", description = "本机租户运营手测宿主装载的实际平台 Provider。", author = new { name = "Qian" }, license = "Apache-2.0",
                @interface = new { displayName = id, category = "testing" }, inject = plugin.Inject.Select(key => key.Name), provides = provides.Select(key => key.Name),
                policy = new { tier = "system", sandbox = "none", authentication = "ON_INSTALL", required = true }, scopes = component is "qian.config" or "qian.tenancy" ? GlobalSettingsScopes : CatalogScopes,
                runtime = new { form = "module", entry = plugin.GetType().FullName + ", " + plugin.GetType().Assembly.GetName().Name }
            }));
        }
    }

    internal static void EnsureReady(PluginLoader loader, string id)
    {
        if (loader.GetState(id, ScopeKey.Global) != PluginState.Ready)
            throw new InvalidOperationException("平台组件未就绪：" + id, loader.GetFailure(id, ScopeKey.Global));
    }

    // 与 Lifecycle 核对的摘要算法相同：按路径排序的「路径、长度、SHA-256」清单再取 SHA-256。
    private static string ArtifactDigest(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> files)
    {
        var inventory = string.Concat(files.OrderBy(file => file.Key, StringComparer.Ordinal).Select(file =>
            file.Key + "\t" + file.Value.Length.ToString(CultureInfo.InvariantCulture) + "\t" + Convert.ToHexStringLower(SHA256.HashData(file.Value.Span)) + "\n"));
        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(inventory)));
    }

    private void Write(string path, string content)
    {
        var file = Path.Combine(_directory.FullName, path); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, content);
    }

    public void Dispose() { try { _directory.Delete(true); } catch (IOException) { } }
}

// 制品签名的平台信任根：每次启动生成 Ed25519 钥，公钥 PEM 按生产形状写进 K8s Secret 挂载目录，配置经 secretRef 引用。
// 也是秘密访问的审计接收端（宿主必须显式提供），手测不落库。
internal sealed class DevTrustRoot : ISecretAccessObserver, IDisposable
{
    private readonly Ed25519PrivateKeyParameters _private = new(new SecureRandom());
    internal DevTrustRoot()
    {
        var spki = Convert.FromHexString("302a300506032b6570032100").Concat(_private.GeneratePublicKey().GetEncoded()).ToArray();
        Fingerprint = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(spki));
        var target = Directory.CreateDirectory(Path.Combine(SecretRoot, "qian", "trust-platform")).FullName;
        File.WriteAllText(Path.Combine(target, "public-key"), "-----BEGIN PUBLIC KEY-----\n" + Convert.ToBase64String(spki) + "\n-----END PUBLIC KEY-----\n");
    }
    internal string SecretRoot { get; } = Directory.CreateTempSubdirectory("qian-dev-tenant-ops-secrets-").FullName;
    internal string Fingerprint { get; }
    internal ArtifactSignature Sign(string digest)
    {
        var message = Convert.FromHexString(digest["sha256:".Length..]);
        var signer = new Ed25519Signer(); signer.Init(true, _private); signer.BlockUpdate(message, 0, message.Length);
        return new ArtifactSignature(Fingerprint, signer.GenerateSignature());
    }
    public ValueTask RecordAsync(SecretAccessEvent access, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    public void Dispose() { try { Directory.Delete(SecretRoot, true); } catch (IOException) { } }
}
