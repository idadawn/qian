using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Qian.Authz;
using Qian.Capabilities;
using Qian.Lifecycle;
using Qian.Profiles.Abstractions;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;

namespace Qian.Platform.Host;

/// <summary>
/// 平台插件树：按合成后的 platform profile 挂载全部启用条目。每个条目的组件文件夹（清单、Schema、metadata、UI 清单）就是它的预装绑定，
/// 交给 Registry 作已安装组件；带 UI 清单的条目以 <see cref="NavigationManifestPlugin"/> 包装后挂载（平台组件的页面因此进平台控制台目录）；
/// Authz 的权限目录由这些预装绑定的 metadata 编译。全部条目 Ready、Registry 启动之后宿主才开始监听。
/// </summary>
public sealed class PlatformRuntime : IHostedService, IAsyncDisposable
{
    private readonly IServiceProvider _services;
    private readonly PlatformComposition _composition;
    private readonly ITenantContext _tenant;
    private readonly Dictionary<string, PluginDefinition> _definitions = new(StringComparer.Ordinal);
    private RegistryRuntimeOptions? _registry;

    internal PlatformRuntime(IServiceProvider services, PlatformComposition composition)
    {
        _services = services; _composition = composition; _tenant = services.GetRequiredService<ITenantContext>();
        Loader = new PluginLoader(services);
        LifecycleOptions = new LifecycleRuntimeOptions { Hooks = composition.LifecycleHooks };
    }

    /// <summary>真实装载器。</summary>
    public PluginLoader Loader { get; }
    /// <summary>宿主启动时合成的 profile 快照（文件层；数据库 scope 层由 Registry 合入）。</summary>
    public ProfileSnapshot Profile => _composition.Snapshot;
    /// <summary>全部条目 Ready 且 Registry 已启动。</summary>
    public bool IsReady { get; private set; }
    internal RegistryRuntimeOptions RegistryOptions => _registry ?? throw new InvalidOperationException("平台插件树尚未组合。");
    internal LifecycleRuntimeOptions LifecycleOptions { get; }

    /// <summary>组件的全局上下文（每次现取：声明式重挂载后不持有失效的旧上下文）；组件不在或已卸载按不存在。</summary>
    public IPluginContext Global(string id)
    {
        try { return Loader.GetContext(id, ScopeKey.Global); }
        catch (Exception error) when (error is KeyNotFoundException or ObjectDisposedException or InvalidOperationException or ServiceNotReadyException)
        { throw new QianErrorException(new QianError(PlatformErrors.NotFound)); }
    }

    /// <summary>
    /// 当前租户视角的组件上下文：租户 scope 有自己的条目（Lifecycle 装进租户、版本可能不同）就用它，否则用全局条目按租户收窄。
    /// 没有租户（平台身份）即未认证，组件不在即不存在。module 应用的能力处理器经它取上下文。
    /// </summary>
    public IPluginContext Tenant(string id)
    {
        if (!Guid.TryParseExact(_tenant.TenantId, "D", out var parsed) || parsed == Guid.Empty) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        var scope = new ScopeKey("tenant", _tenant.TenantId!);
        try
        {
            return Loader.DumpConfig().Any(entry => entry.Id == id && entry.Scope == scope)
                ? Loader.GetContext(id, scope) : Loader.GetContext(id, ScopeKey.Global).Scoped(scope);
        }
        catch (Exception error) when (error is KeyNotFoundException or ObjectDisposedException or InvalidOperationException or ServiceNotReadyException)
        { throw new QianErrorException(new QianError(PlatformErrors.NotFound)); }
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var validator = _services.GetRequiredService<SurfaceValidator>();
        var artifacts = Profile.Entries.ToDictionary(entry => entry.Id, entry => Artifact.Read(entry.ComponentDirectory), StringComparer.Ordinal);
        // 第一遍：组件文件夹按清单声明校验，只为编译权限目录（Authz 的 Provider 要先拿到目录才能创建）。
        var declared = Profile.Entries.Select(entry => Bind(entry, artifacts[entry.Id], Definition(entry, new DeclaredOnly(entry)), validator)).ToArray();
        var compiler = new PermissionCatalogCompiler(validator);
        var permissions = declared.Where(binding => binding.Metadata.Metadata.Keys.Any(path => path.Split('/')[^1] == "permissions.json"))
            .Select(binding => compiler.FromInstalled(binding.Metadata)).ToArray();
        var activation = new PlatformActivation(_services, Loader, declared, permissions);
        // 第二遍：目录创建实际 Provider，用实际定义再校验一次（清单 inject 必须等于 Provider 的 Inject，清单 provides 必须都在定义里），得到交给 Registry 的预装绑定。
        var installed = new List<RegistryComponentBinding>();
        foreach (var entry in Profile.Entries)
        {
            var plugin = _composition.Components[entry.Id].Create(activation);
            var definition = Definition(entry, plugin);
            var binding = Bind(entry, artifacts[entry.Id], definition, validator);
            installed.Add(binding);
            _definitions[entry.Id] = UiManifest(entry) ? definition with { Plugin = new NavigationManifestPlugin(plugin, binding.Metadata, validator) } : definition;
        }
        _registry = new RegistryRuntimeOptions
        {
            InstalledComponents = installed, Runtime = Loader, Profiles = _composition.ProfileLoader, Profile = _composition.LoadOptions, Surfaces = validator,
            Resolve = Resolve
        };
        await Loader.MountAsync(Profile.MountOrder.Select(key => _definitions[key["global/".Length..]]).ToArray(), cancellationToken).ConfigureAwait(false);
        foreach (var entry in Profile.Entries)
            if (Loader.GetState(entry.Id, ScopeKey.Global) != PluginState.Ready)
                throw new InvalidOperationException("平台插件树的条目未就绪：" + entry.Id + "（" + entry.Plugin + "）", Loader.GetFailure(entry.Id, ScopeKey.Global));
        if (_definitions.ContainsKey("registry")) await Loader.GetContext("registry", ScopeKey.Global).Registry().StartAsync(cancellationToken).ConfigureAwait(false);
        IsReady = true;
        var logger = _services.GetRequiredService<ILogger<PlatformRuntime>>();
        PlatformLog.Ready(logger, _definitions.Count);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => DisposeAsync().AsTask();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        IsReady = false;
        await Loader.DisposeAsync().ConfigureAwait(false);
    }

    // Registry 合入数据库 scope 层后按同一目录解析条目：同一 Provider（带 UI 清单的同一包装）、条目自己的 scope、层号与配置。
    private PluginDefinition Resolve(ResolvedProfileEntry entry)
    {
        var plugin = entry.Plugin.Split('@', 2)[0];
        var known = _definitions.Values.FirstOrDefault(definition => definition.Entry.Plugin == plugin)
            ?? throw new InvalidOperationException("Registry 请求的组件不在平台宿主的已安装组件里：" + entry.Plugin);
        var scope = entry.Scope == "global" ? ScopeKey.Global : new ScopeKey(entry.Scope.Split(':', 2)[0], entry.Scope.Split(':', 2)[1]);
        return known with { Entry = new PluginEntry(entry.Id, plugin, entry.Plugin.Split('@', 2)[1], scope, entry.Layer, entry.Config) };
    }

    private static PluginDefinition Definition(ResolvedProfileEntry entry, IQianPlugin plugin)
    {
        var reference = entry.Plugin.Split('@', 2);
        return new PluginDefinition(new PluginEntry(entry.Id, reference[0], reference[1], ScopeKey.Global, entry.Layer, entry.Config.Clone()), plugin,
            entry.Provides.Select(name => new ServiceKey(name)).ToArray());
    }

    // 同一份文件快照经平台静态校验成为预装绑定。命令型工具的绑定只认组件代码派生的能力（组合时从入口程序集的生成元数据读出），不在其中即拒绝。
    private RegistryComponentBinding Bind(ResolvedProfileEntry entry, Artifact artifact, PluginDefinition definition, SurfaceValidator validator) =>
        RegistryComponentBinding.FromArtifact(definition, artifact.Files, artifact.Digest, validator, new SurfaceValidationOptions
        {
            Profile = "platform", ExecutionPlane = "server", CatalogComplete = true, Capabilities = _composition.Capabilities[entry.Id]
        });

    // 组件文件夹即制品：按路径排序的「路径、长度、SHA-256」清单再取 SHA-256（与 Lifecycle 核对制品的摘要算法相同）。构建产物与依赖目录不属于制品。
    private sealed record Artifact(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Files, string Digest)
    {
        internal static Artifact Read(string folder)
        {
            var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .Where(file => !Path.GetRelativePath(folder, file).Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or "node_modules" or "dist"))
                .ToDictionary(file => Path.GetRelativePath(folder, file).Replace(Path.DirectorySeparatorChar, '/'), file => new ReadOnlyMemory<byte>(File.ReadAllBytes(file)), StringComparer.Ordinal);
            var inventory = string.Concat(files.OrderBy(file => file.Key, StringComparer.Ordinal).Select(file =>
                file.Key + "\t" + file.Value.Length.ToString(CultureInfo.InvariantCulture) + "\t" + Convert.ToHexStringLower(SHA256.HashData(file.Value.Span)) + "\n"));
            return new Artifact(files, "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(inventory))));
        }
    }

    private static bool UiManifest(ResolvedProfileEntry entry) =>
        entry.Manifest.TryGetProperty("surfaces", out var surfaces) && surfaces.ValueKind == JsonValueKind.Object && surfaces.TryGetProperty("ui", out _);

    // 第一遍的声明实例只携带清单的 inject，不挂载；真正挂载的是目录创建的 Provider。
    private sealed class DeclaredOnly(ResolvedProfileEntry entry) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject { get; } = entry.Inject.Select(name => new ServiceKey(name)).ToArray();
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) =>
            throw new InvalidOperationException("预装绑定的声明实例不能挂载。");
    }
}

internal static partial class PlatformLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "平台插件树已就绪：{Count} 个条目。")]
    internal static partial void Ready(ILogger logger, int count);

}
