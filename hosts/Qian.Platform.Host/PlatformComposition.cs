using System.Reflection;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.EventBus;
using Kun.EventBus.DependencyInjection;
using Kun.Mediator;
using Kun.Mediator.DependencyInjection;
using Kun.MultiTenancy;
using Kun.Security.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Authz.AspNetCore;
using Qian.Capabilities.AspNetCore;
using Qian.Config;
using Qian.Config.Secrets;
using Qian.Data;
using Qian.Events;
using Qian.Identity.Abstractions;
using Qian.Lifecycle;
using Qian.Plugin.Sdk;
using Qian.Plugin.Sdk.AspNetCore;
using Qian.Profiles;
using Qian.Profiles.Abstractions;
using Qian.Registry;
using Qian.Runtime.Errors.AspNetCore;
using Qian.Surfaces;
using Qian.Tenancy;

namespace Qian.Platform.Host;

/// <summary>
/// 平台宿主的组合：先收扩展登记（module 应用、S1-DEP-011 并入的身份分支等），再合成 platform profile、核对目录、登记宿主 DI。
/// Kun 的事件类型表只认第一次 <c>AddKunEventBus</c>、中介者只认最后一次 <c>AddKunMediator</c>，所以扩展的事件与处理器登记都并进这里的同一次调用。
/// </summary>
public sealed class PlatformHostComposition
{
    private readonly List<PlatformComponent> _extra = [];
    private readonly List<string> _directories = [];
    private readonly List<string> _patches = [];
    private readonly List<Action<IEventTypeRegistrar>> _events = [];
    private readonly List<Action<KunMediatorBuilder>> _mediator = [];
    private readonly List<Func<string, string, IPluginLifecycleHooks?>> _hooks = [];
    private readonly List<PathString> _operations = [new("/api/tenancy")];
    private readonly List<Action<IEndpointRouteBuilder, PlatformRuntime>> _operationEndpoints = [];
    private readonly List<Action<IEndpointRouteBuilder, PlatformRuntime>> _endpoints = [];
    private PlatformComposition? _composed;

    internal PlatformHostComposition(WebApplicationBuilder builder, PlatformHostSettings settings) { Builder = builder; Settings = settings; }

    /// <summary>宿主构建器。</summary>
    public WebApplicationBuilder Builder { get; }
    /// <summary>宿主设置。</summary>
    public PlatformHostSettings Settings { get; }
    /// <summary>宿主 DI。</summary>
    public IServiceCollection Services => Builder.Services;
    /// <summary>合成后的 profile 快照；组合完成前访问即抛错。</summary>
    public ProfileSnapshot Profile => (_composed ?? throw new InvalidOperationException("平台宿主尚未组合。")).Snapshot;

    /// <summary>登记一个目录项（module 应用或扩展组件）；不能与内置目录重名。</summary>
    public PlatformHostComposition AddComponent(PlatformComponent component)
    {
        ArgumentNullException.ThrowIfNull(component); Open();
        if (PlatformComponents.BuiltIn.Concat(_extra).Any(item => item.Plugin == component.Plugin)) throw new InvalidOperationException("目录里已有组件 " + component.Plugin + "。");
        _extra.Add(component); return this;
    }
    /// <summary>额外的组件根目录（随镜像发布的 module 应用组件文件夹）。</summary>
    public PlatformHostComposition AddComponentDirectory(string path) { ArgumentException.ThrowIfNullOrWhiteSpace(path); Open(); _directories.Add(Path.GetFullPath(path)); return this; }
    /// <summary>扩展 overlay：在宿主 overlay 之后、环境 overlay 之前应用（例如把 module 应用的条目加进全局 scope）。</summary>
    public PlatformHostComposition AddPatch(string path) { ArgumentException.ThrowIfNullOrWhiteSpace(path); Open(); _patches.Add(Path.GetFullPath(path)); return this; }
    /// <summary>并进同一次 <c>AddKunEventBus</c> 的事件类型登记。</summary>
    public PlatformHostComposition AddEvents(Action<IEventTypeRegistrar> events) { ArgumentNullException.ThrowIfNull(events); Open(); _events.Add(events); return this; }
    /// <summary>并进同一次 <c>AddKunMediator</c> 的处理器登记（module 应用的能力处理器、扩展的事件处理器）。</summary>
    public PlatformHostComposition AddMediator(Action<KunMediatorBuilder> mediator) { ArgumentNullException.ThrowIfNull(mediator); Open(); _mediator.Add(mediator); return this; }
    /// <summary>Lifecycle 的 module 钩子：按组件 ID 与精确版本返回实现，没有返回 null。</summary>
    public PlatformHostComposition AddLifecycleHooks(Func<string, string, IPluginLifecycleHooks?> hooks) { ArgumentNullException.ThrowIfNull(hooks); Open(); _hooks.Add(hooks); return this; }
    /// <summary>
    /// 运营接口：<paramref name="map"/> 映射到运营端点组（在租户运营接口之后），组里的端点只收受众 <c>qian-operations</c> 的 S2S——受众按路由匹配到的端点选，
    /// 不按路径写法。<paramref name="prefix"/> 声明这组端点所在的路径范围，与网关路由表里指向运营簇的前缀对应：启动时核对组里的端点都在声明的前缀下、前缀下的端点都在组里，
    /// 没有匹配到端点的请求也按前缀选运营受众（得到 404 / 405 而不是 401）。S1-DEP-011 并入身份分支后，开通申请的运营接口（<c>/api/signup/applications</c>、<c>/api/signup/meta</c>）经这里挂上。
    /// </summary>
    public PlatformHostComposition AddOperations(PathString prefix, Action<IEndpointRouteBuilder, PlatformRuntime>? map = null)
    {
        if (!prefix.HasValue || prefix.Value == "/" || prefix.Value.EndsWith('/')) throw new ArgumentException("运营前缀必须是非根路径，且不以斜杠结尾。", nameof(prefix));
        Open();
        if (!_operations.Contains(prefix)) _operations.Add(prefix);
        if (map is not null) _operationEndpoints.Add(map);
        return this;
    }
    /// <summary>其它端点（走平台受众）；在平台表面之后映射，不能放在运营前缀下（启动时核对）。</summary>
    public PlatformHostComposition MapEndpoints(Action<IEndpointRouteBuilder, PlatformRuntime> map) { ArgumentNullException.ThrowIfNull(map); Open(); _endpoints.Add(map); return this; }

    internal IReadOnlyList<PathString> OperationPrefixes => _operations;
    internal IReadOnlyList<Action<IEndpointRouteBuilder, PlatformRuntime>> OperationEndpoints => _operationEndpoints;
    internal IReadOnlyList<Action<IEndpointRouteBuilder, PlatformRuntime>> Endpoints => _endpoints;

    private void Open() { if (_composed is not null) throw new InvalidOperationException("平台宿主已组合，不能再登记扩展。"); }

    // 合成 profile → 每个启用条目在目录里找 Provider 并核对清单 → 登记宿主 DI 与插件树运行时。
    internal void Compose()
    {
        Open();
        var validator = new SurfaceValidator();
        var loader = new ProfileLoader(validator);
        var options = new ProfileLoadOptions
        {
            WorkingDirectory = Settings.Root, Profile = "platform", IncludeHomePatch = false,
            Patches = [Settings.HostOverlay, .. _patches, .. Settings.Patches], ComponentDirectories = [.. _directories, .. Settings.ComponentDirectories]
        };
        var result = loader.Load(options);
        if (!result.IsValid) throw new InvalidOperationException("platform profile 装配失败：" + JsonSerializer.Serialize(result.Diagnostics.Where(item => item.Severity == "error").Select(item => item.Error)));
        var snapshot = result.Snapshot!;
        var catalog = PlatformComponents.BuiltIn.Concat(_extra).ToDictionary(item => item.Plugin, StringComparer.Ordinal);
        var components = new Dictionary<string, PlatformComponent>(StringComparer.Ordinal);
        var capabilities = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var missing = new List<string>();
        foreach (var entry in snapshot.Entries)
        {
            if (entry.Scope != "global") throw new InvalidOperationException("文件层的条目只能在 global scope：" + entry.Id);
            var id = entry.Plugin.Split('@', 2)[0];
            if (!catalog.TryGetValue(id, out var component)) { missing.Add(entry.Id + "（" + entry.Plugin + "）"); continue; }
            var declared = entry.Manifest.TryGetProperty("runtime", out var runtime) && runtime.TryGetProperty("entry", out var value) ? value.GetString() : null;
            if (declared != component.Entry)
                throw new InvalidOperationException("条目 " + entry.Id + " 的清单 runtime.entry 是 " + (declared ?? "（空）") + "，平台宿主的目录是 " + component.Entry + "。");
            components[entry.Id] = component;
            capabilities[entry.Id] = CodeCapabilities(component.Entry, id);
        }
        if (missing.Count > 0)
            throw new InvalidOperationException("平台宿主没有这些条目的 Provider，在环境 overlay 里禁用它们（disabled: true）：" + string.Join("、", missing));
        _composed = new PlatformComposition(snapshot, options, loader, components, capabilities, Hooks);
        Register(_composed);
    }

    // 组件代码派生的可调用能力：SDK 源生成器写进入口程序集的元数据（与 qian-plugin check 核对清单用的是同一来源），不实例化组件。
    private static string[] CodeCapabilities(string entry, string componentId)
    {
        var type = Type.GetType(entry, throwOnError: false) ?? throw new InvalidOperationException("找不到组件入口类型：" + entry);
        return type.Assembly.GetCustomAttributes<QianCodeMetadataAttribute>()
            .Select(attribute => ((IComponentCodeMetadataFactory)Activator.CreateInstance(attribute.FactoryType)!).Create())
            .Where(metadata => metadata.ComponentId == componentId).SelectMany(metadata => metadata.Capabilities.Select(contract => contract.Id)).ToArray();
    }

    private IPluginLifecycleHooks? Hooks(string id, string version) => _hooks.Select(hooks => hooks(id, version)).FirstOrDefault(hook => hook is not null);

    private void Register(PlatformComposition composed)
    {
        var services = Services; var settings = Settings;
        services.AddSingleton(settings);
        services.TryAddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        services.AddHttpContextAccessor();
        services.AddQianErrors();
        services.AddKunSecurity();
        services.AddScoped<ICurrentUser, GatewayCurrentUser>();
        services.AddKunEventBus(registrar =>
        {
            registrar.RegisterTenancyEvents().RegisterAuthzEvents().RegisterRegistryEvents().RegisterLifecycleEvents();
            foreach (var events in _events) events(registrar);
        });
        services.AddKunMediator(registry =>
        {
            registry.AddQianTenancyEvents().AddQianAuthzEvents().AddQianRegistryEvents().AddQianLifecycleEvents();
            foreach (var mediator in _mediator) mediator(registry);
        });
        // 数据会话的 Outbox 写成 v1 信封（只写不投递）；events 条目的 broker / 领取方式由它的登记再配。
        services.AddQianEvents();
        // 迁移任务（--migrate-only）用属主连接执行包内迁移；在线副本不挂属主凭据，这个解析只在迁移时调用。
        services.AddQianDataMigrations(options => options.ResolveConnection = (_, _) => settings.Connection("Migrations"));
        PlatformComponents.RegisterShared(services);
        // 端点的权限判定器与命令管道读宿主的同一份活动登记表；Authz Provider 发现宿主已有登记表时共用同一实例。
        services.AddSingleton<IPermissionRegistry, PermissionRegistry>();
        services.AddSingleton<Kun.Authorization.IPermissionChecker>(sp => new RbacPermissionChecker(sp.GetRequiredService<IDataService>(),
            sp.GetRequiredService<ITenantContext>(), sp.GetRequiredService<IPermissionRegistry>()));
        services.AddQianHttpContracts();
        services.AddQianPluginEndpoints(sp => sp.GetRequiredService<PlatformRuntime>().Global("registry"));
        services.AddQianAuthzEnforcement(http => PlatformHost.Scoped(http, "authz"), options => options.Production = true);
        if (settings.SecretMountRoot is { } mount)
        {
            // 平台信任根：公钥 PEM 在 K8s Secret 里，Trust:PlatformKeys 经 secretRef 引用；每次解析写平台审计链。
            services.AddSingleton<ISecretAccessObserver>(sp => new PlatformSecretAudit(() => sp.GetRequiredService<PlatformRuntime>().Global("config")));
            services.AddQianSecrets(new SecretResolverOptions
            {
                MountRoot = mount,
                Bindings = [new SecretBinding("secretRef://platform/trust/platform", new KubernetesSecretLocation("qian", "trust-platform", "public-key"))],
                Policies = [new SecretAccessPolicy("qian.lifecycle", [new("trust/*", "platform")], [new("trust/*", "platform")])]
            });
        }
        GatewayS2s.Add(services, settings, _operations);
        services.AddAuthorizationBuilder().SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNameCaseInsensitive = false;
            options.SerializerOptions.RespectRequiredConstructorParameters = true;
            options.SerializerOptions.RespectNullableAnnotations = true;
        });
        foreach (var entry in composed.Snapshot.Entries)
            composed.Components[entry.Id].Register?.Invoke(new PlatformServices(services, Builder.Configuration, settings, entry));
        services.AddSingleton(sp => new PlatformRuntime(sp, composed));
        services.AddHostedService(sp => sp.GetRequiredService<PlatformRuntime>());
    }
}

/// <summary>组合结果：profile 快照、装配输入（Registry 合入数据库 scope 层时复用）、每个条目的目录项与代码派生能力、Lifecycle 钩子。</summary>
internal sealed record PlatformComposition(ProfileSnapshot Snapshot, ProfileLoadOptions LoadOptions, ProfileLoader ProfileLoader,
    IReadOnlyDictionary<string, PlatformComponent> Components, IReadOnlyDictionary<string, IReadOnlyList<string>> Capabilities,
    Func<string, string, IPluginLifecycleHooks?> LifecycleHooks);
