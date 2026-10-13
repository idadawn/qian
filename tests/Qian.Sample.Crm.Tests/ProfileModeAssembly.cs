using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Qian.Capabilities;
using Qian.Profiles;
using Qian.Profiles.Abstractions;
using Qian.Registry;
using Qian.Runtime;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;

namespace Qian.Sample.Crm.Tests;

// T23 的实际宿主装配：profile 决定挂载顺序，所有叶节点均为生产 Provider，没有骨架或替身服务。
internal sealed class ProfileModeAssembly(string profile, string repositoryRoot) : ICrmRuntimeAssembly, IDisposable
{
    private static readonly string[] AllowedScopes = ["global", "tenant"];
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("qian-t23-host-");
    private readonly Dictionary<string, PluginDefinition> _definitions = new(StringComparer.Ordinal);
    internal ProfileSnapshot Snapshot { get; private set; } = null!;
    internal IReadOnlyDictionary<string, string> BusinessTypes { get; private set; } = null!;
    internal IReadOnlyDictionary<string, PluginDefinition> Definitions => _definitions;

    public ValueTask<IReadOnlyList<PluginDefinition>> ComposeAsync(PluginLoader runtime, IReadOnlyList<PluginDefinition> businessProviders, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (profile is not ("minimal" or "platform")) throw new InvalidOperationException("T23 profile 无效。");
        BusinessTypes = businessProviders.ToDictionary(item => item.Entry.Id, item => EntryType(item.Plugin), StringComparer.Ordinal);
        foreach (var definition in businessProviders) _definitions.Add(definition.Entry.Id, definition);
        var validator = new SurfaceValidator(); var composer = new ProfileLoader(validator);
        var installed = new List<RegistryComponentBinding>();
        var options = new ProfileLoadOptions { WorkingDirectory = _directory.FullName, Profile = profile, IncludeHomePatch = false };
        if (profile == "platform")
        {
            Add("registry", "qian.registry", new RegistryPlugin(new RegistryRuntimeOptions
            {
                Runtime = runtime, Profiles = composer, Profile = options, Surfaces = validator, InstalledComponents = installed,
                Resolve = entry => Resolve(entry)
            }), [RegistryDefinition.Key]);
            Add("capabilities", "qian.capabilities", new CapabilitiesPlugin(), [CapabilitiesDefinition.Key, CapabilitiesDefinition.ContractsKey, CapabilitiesDefinition.NavigationKey]);
        }
        foreach (var definition in _definitions.Values)
        {
            var platformPlugin = definition.Entry.Id is "registry" or "capabilities";
            Write($"plugins/{definition.Entry.Id}/.qian-plugin/plugin.json", JsonSerializer.Serialize(new
            {
                id = definition.Entry.Plugin, version = definition.Entry.Version, description = "T23 宿主装载同一份已实现 Provider 的组件。",
                author = new { name = "Qian" }, license = "Apache-2.0", @interface = new { displayName = definition.Entry.Id, category = "testing" },
                inject = definition.Plugin.Inject.Select(key => key.Name), provides = definition.Provides.Select(key => key.Name), scopes = AllowedScopes,
                policy = new { tier = platformPlugin ? "system" : "official", sandbox = "none", authentication = "ON_INSTALL", required = platformPlugin },
                runtime = new { form = "module", entry = EntryType(definition.Plugin) }
            }));
        }
        var patch = JsonSerializer.Serialize(_definitions.Values.Select(item => new { id = item.Entry.Id, plugin = item.Entry.Plugin + "@" + item.Entry.Version, config = new { } }));
        if (profile == "minimal")
        {
            Copy("profiles/minimal"); Copy("plugins/qian.bundle.minimal");
            // 保留仓库交付的空 minimal 组合包，通过 profile patch 装入同组业务 Provider。
            Write("profiles/minimal/qian.patch.yml", patch);
        }
        else
        {
            Write("profiles/platform/qian.profile.yml", """{"name":"platform","bundles":["qian.bundle.tck-platform@1.0.0"],"patch":"./qian.patch.yml","hmr":{"surfaces":true,"code":false},"build":{"aot":false},"hooks":{"allowCommand":false}}""");
            Write("profiles/platform/qian.patch.yml", "[]");
            Write("plugins/qian.bundle.tck-platform/.qian-plugin/plugin.json", """{"id":"qian.bundle.tck-platform","version":"1.0.0","description":"T23 平台宿主的真实业务与 Registry/Capabilities 组合。","author":{"name":"Qian"},"license":"Apache-2.0","interface":{"displayName":"T23 平台","category":"testing"},"inject":[],"provides":[],"scopes":["global"],"policy":{"tier":"official","sandbox":"none","authentication":"ON_INSTALL"},"surfaces":{"patch":"./config/qian.patch.yml"}}""");
            Write("plugins/qian.bundle.tck-platform/config/qian.patch.yml", patch);
        }
        var contract = CapabilityContractCompiler.CreateOpenApi([CreateCustomerCommand.QianCapability, ListCustomersQuery.QianCapability, GetCustomerQuery.QianCapability], validator, new ToolContractHasher());
        Write("plugins/crm/metadata/contracts.json", """[{"id":"qian.sample.crm.api/v1","kind":"openapi","path":"./contracts/http-v1.openapi.json"}]""");
        Write("plugins/crm/contracts/http-v1.openapi.json", contract.GetRawText());
        var composed = composer.Load(options);
        if (!composed.IsValid || composed.Diagnostics.Count != 0) throw new InvalidOperationException(JsonSerializer.Serialize(composed.Diagnostics));
        Snapshot = composed.Snapshot!;
        foreach (var entry in Snapshot.Entries)
        {
            var definition = Resolve(entry); _definitions[entry.Id] = definition;
            if (entry.Manifest.TryGetProperty("x-skeleton", out _)) throw new InvalidOperationException("T23 禁止骨架组件。");
            if (entry.Manifest.GetProperty("runtime").GetProperty("entry").GetString() != EntryType(definition.Plugin)) throw new InvalidOperationException("清单必须指向实际 Provider。");
            var validation = validator.ValidateComponent(entry.ComponentDirectory, new SurfaceValidationOptions { Profile = profile, ExecutionPlane = "server", CatalogComplete = true });
            if (!validation.IsValid) throw new InvalidOperationException(JsonSerializer.Serialize(validation.Diagnostics));
            if (profile == "minimal") continue; // 独立模式不调用平台绑定或注册表。
            var files = Directory.EnumerateFiles(entry.ComponentDirectory, "*", SearchOption.AllDirectories).ToDictionary(
                file => Path.GetRelativePath(entry.ComponentDirectory, file).Replace(Path.DirectorySeparatorChar, '/'), file => new ReadOnlyMemory<byte>(File.ReadAllBytes(file)), StringComparer.Ordinal);
            var inventory = string.Concat(files.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => item.Key + "\t" + item.Value.Length.ToString(CultureInfo.InvariantCulture) + "\t" + Convert.ToHexStringLower(SHA256.HashData(item.Value.Span)) + "\n"));
            installed.Add(RegistryComponentBinding.FromArtifact(definition, files, "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(inventory))), validator,
                new SurfaceValidationOptions { Profile = profile, ExecutionPlane = "server", CatalogComplete = true }));
        }
        return ValueTask.FromResult<IReadOnlyList<PluginDefinition>>(Snapshot.MountOrder.Select(key => _definitions[key["global/".Length..]]).ToArray());

        void Add(string id, string component, IQianPlugin plugin, ServiceKey[] provides) => _definitions.Add(id,
            new PluginDefinition(new PluginEntry(id, component, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), plugin, provides));
    }
    public async ValueTask ReadyAsync(PluginLoader runtime, CancellationToken cancellationToken)
    {
        if (profile == "platform") await runtime.GetContext("registry", ScopeKey.Global).Registry().StartAsync(cancellationToken);
    }
    private PluginDefinition Resolve(ResolvedProfileEntry entry)
    {
        var known = _definitions[entry.Id];
        if (entry.Plugin != known.Entry.Plugin + "@" + known.Entry.Version ||
            !entry.Inject.Order(StringComparer.Ordinal).SequenceEqual(known.Plugin.Inject.Select(key => key.Name).Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
            !entry.Provides.Order(StringComparer.Ordinal).SequenceEqual(known.Provides.Select(key => key.Name).Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw new InvalidOperationException("profile 与实际 Provider 不一致。");
        var scope = entry.Scope == "global" ? ScopeKey.Global : new ScopeKey(entry.Scope.Split(':', 2)[0], entry.Scope.Split(':', 2)[1]);
        return known with { Entry = known.Entry with { Scope = scope, Layer = entry.Layer, Config = entry.Config } };
    }
    private static string EntryType(IQianPlugin plugin) => plugin.GetType().FullName + ", " + plugin.GetType().Assembly.GetName().Name;
    private void Write(string relative, string text) { var file = Path.Combine(_directory.FullName, relative); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, text); }
    private void Copy(string relative)
    {
        foreach (var original in Directory.EnumerateFiles(Path.Combine(repositoryRoot, relative), "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(_directory.FullName, Path.GetRelativePath(repositoryRoot, original)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(original, target);
        }
    }
    public void Dispose() => _directory.Delete(true);
}
