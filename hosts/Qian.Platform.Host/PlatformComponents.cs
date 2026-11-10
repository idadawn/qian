using System.Text.Json;
using Kun.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Qian.Audit;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Capabilities;
using Qian.Config.Apollo;
using Qian.Data;
using Qian.Events;
using Qian.Lifecycle;
using Qian.Profiles.Abstractions;
using Qian.Registry;
using Qian.Runtime;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;
using Qian.Tenancy;
using StackExchange.Redis;

namespace Qian.Platform.Host;

/// <summary>
/// 宿主认识的一个组件：profile 条目的组件 id、清单 <c>runtime.entry</c> 应指向的入口类型、宿主登记（数据上下文、宿主服务）与实例工厂。
/// profile 里每个启用的条目都必须在目录里有对应项，否则宿主不启动；清单与实际 Provider 的 inject 不一致同样拒绝（防止 profile 与代码漂移）。
/// </summary>
public sealed class PlatformComponent
{
    /// <summary>新建目录项。</summary>
    /// <param name="plugin">组件 id，如 <c>qian.tenancy</c>。</param>
    /// <param name="entry">清单 <c>runtime.entry</c> 的期望值，如 <c>Qian.Tenancy.TenancyComponent, Qian.Tenancy</c>。</param>
    /// <param name="create">挂载时创建 Provider 实例。</param>
    /// <param name="register">组合时向宿主 DI 登记该组件需要的服务（数据上下文等）；条目被禁用时不调用。</param>
    public PlatformComponent(string plugin, string entry, Func<PlatformActivation, IQianPlugin> create, Action<PlatformServices>? register = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plugin); ArgumentException.ThrowIfNullOrWhiteSpace(entry); ArgumentNullException.ThrowIfNull(create);
        Plugin = plugin; Entry = entry; Create = create; Register = register;
    }
    /// <summary>组件 id。</summary>
    public string Plugin { get; }
    /// <summary>清单 <c>runtime.entry</c> 的期望值。</summary>
    public string Entry { get; }
    /// <summary>实例工厂。</summary>
    public Func<PlatformActivation, IQianPlugin> Create { get; }
    /// <summary>宿主登记。</summary>
    public Action<PlatformServices>? Register { get; }
}

/// <summary>组合期的登记上下文：一个启用的 profile 条目、宿主 DI 与按登录绑定数据上下文的辅助方法。</summary>
public sealed class PlatformServices
{
    internal PlatformServices(IServiceCollection services, IConfiguration configuration, PlatformHostSettings settings, ResolvedProfileEntry entry)
    {
        Services = services; Configuration = configuration; Settings = settings; Entry = entry;
    }
    /// <summary>宿主 DI。</summary>
    public IServiceCollection Services { get; }
    /// <summary>宿主配置根。</summary>
    public IConfiguration Configuration { get; }
    /// <summary>宿主设置。</summary>
    public PlatformHostSettings Settings { get; }
    /// <summary>合成后的 profile 条目（含最终配置）。</summary>
    public ResolvedProfileEntry Entry { get; }

    /// <summary>把业务上下文绑到平台应用登录（受限角色，按租户 RLS）；连接在每次建上下文时现读 Secret 文件。</summary>
    public void AddAppData<TContext>(string schema) where TContext : QianDbContext => AddData<TContext>(schema, "App");

    /// <summary>把上下文绑到 <paramref name="connection"/> 指名的独立登录（平台授权、平台审计）。</summary>
    public void AddData<TContext>(string schema, string connection) where TContext : QianDbContext
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema); ArgumentException.ThrowIfNullOrWhiteSpace(connection);
        var settings = Settings;
        Services.AddQianData<TContext>((_, options) => options.UseNpgsql(settings.Connection(connection)).UseKunSchema(schema));
    }

    /// <summary>条目配置里的字符串字段（逐级取对象成员），没有即 null。</summary>
    public string? ConfigString(params string[] path)
    {
        var current = Entry.Config;
        foreach (var name in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current)) return null;
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }
}

/// <summary>挂载期的上下文：宿主容器、真实装载器、预装绑定，以及从预装组件 metadata 编译出的权限目录。</summary>
public sealed class PlatformActivation
{
    internal PlatformActivation(IServiceProvider services, PluginLoader runtime, IReadOnlyList<RegistryComponentBinding> installed, IReadOnlyList<PermissionCatalog> permissions)
    {
        Services = services; Runtime = runtime; Installed = installed; Permissions = permissions;
    }
    /// <summary>宿主容器。</summary>
    public IServiceProvider Services { get; }
    /// <summary>真实装载器。</summary>
    public PluginLoader Runtime { get; }
    /// <summary>预装绑定（profile 条目的组件文件夹）。</summary>
    public IReadOnlyList<RegistryComponentBinding> Installed { get; }
    /// <summary>预装组件 metadata（<c>metadata/permissions.json</c> 与 <c>roles.json</c>）编译出的权限目录。</summary>
    public IReadOnlyList<PermissionCatalog> Permissions { get; }
}

/// <summary>平台插件树的内置目录：S1 平台表面与运营接口需要的实际 Provider。</summary>
public static class PlatformComponents
{
    /// <summary>
    /// Data / Events / Tenancy / Authz / Audit / Config / Registry / Capabilities / Lifecycle。除 Authz 外都用清单 <c>runtime.entry</c> 指向的分发入口；
    /// Authz 的分发入口不预置权限目录，平台宿主改用同一 Provider 的程序化入口，传入预装组件 metadata 编译出的目录（inject 与清单相同）。
    /// </summary>
    public static IReadOnlyList<PlatformComponent> BuiltIn { get; } =
    [
        new("qian.data", "Qian.Data.DataPlugin, Qian.Data", _ => new DataPlugin(), services =>
        {
            // 平台宿主按登录分别绑定上下文（应用登录、平台授权登录、平台审计登录）：data 条目不能再用 connection 把它们收窄到一个引用。
            if (services.ConfigString("connection") is not null)
                throw new InvalidOperationException("platform 宿主按登录分别绑定数据上下文，data 条目不能指定 connection；用宿主 overlay 的 data 配置。");
        }),
        new("qian.events", "Qian.Events.EventsPlugin, Qian.Events", _ => new EventsPlugin(), RegisterEvents),
        new("qian.tenancy", "Qian.Tenancy.TenancyComponent, Qian.Tenancy", _ => new TenancyComponent(),
            services => services.AddAppData<TenancyDbContext>(TenancyDbContext.Schema)),
        new("qian.authz", "Qian.Authz.AuthzComponent, Qian.Authz", activation => new AuthzPlugin(activation.Permissions), services =>
        {
            services.AddAppData<AuthzDbContext>(AuthzDbContext.Schema);
            // 配置了平台授权登录（platformRole）才有平台超管旁路与平台控制台的只读判定；登录凭据只在这里绑定。
            if (services.ConfigString("platformRole") is not null) services.AddData<PlatformAuthzDbContext>(AuthzDbContext.Schema, "PlatformAuthz");
        }),
        new("qian.audit", "Qian.Audit.AuditComponent, Qian.Audit", _ => new AuditComponent(), services =>
        {
            services.AddAppData<AuditDbContext>(AuditDbContext.Schema);
            if (services.ConfigString("chain", "platformRole") is not null) services.AddData<PlatformAuditDbContext>(AuditDbContext.Schema, "PlatformAudit");
        }),
        new("qian.config", "Qian.Config.Apollo.ConfigurationPlugin, Qian.Config.Apollo", _ => new ConfigurationPlugin()),
        new("qian.registry", "Qian.Registry.RegistryComponent, Qian.Registry", _ => new RegistryComponent(), services =>
        {
            services.AddAppData<RegistryDbContext>(RegistryDbContext.Schema);
            services.Services.AddSingleton(sp => sp.GetRequiredService<PlatformRuntime>().RegistryOptions);
        }),
        new("qian.capabilities", "Qian.Capabilities.CapabilitiesComponent, Qian.Capabilities", _ => new CapabilitiesComponent(), services =>
        {
            services.AddAppData<CapabilitiesDbContext>(CapabilitiesDbContext.Schema);
            services.Services.AddQianCapabilities();
        }),
        new("qian.lifecycle", "Qian.Lifecycle.LifecycleComponent, Qian.Lifecycle", _ => new LifecycleComponent(), services =>
        {
            services.AddAppData<LifecycleDbContext>(LifecycleDbContext.Schema);
            services.Services.AddSingleton(sp => sp.GetRequiredService<PlatformRuntime>().LifecycleOptions);
        })
    ];

    // Outbox 的领取方式与 broker 引用由条目配置决定，宿主装配必须与之一致（EventsPlugin 挂载时核对）：redis 领取要 Redis 连接，broker 要 AMQP 地址的 Secret 文件。
    private static void RegisterEvents(PlatformServices services)
    {
        var mode = services.ConfigString("outbox", "lock") ?? "skip-locked";
        if (mode == "redis")
        {
            var settings = services.Settings;
            services.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(settings.Connection("Redis")));
        }
        services.Services.AddQianEvents(options => options.OutboxLock = mode == "redis" ? OutboxLockMode.Redis : OutboxLockMode.SkipLocked);
        if (services.ConfigString("broker") is { } broker)
        {
            var settings = services.Settings;
            services.Services.AddQianEventsBroker(_ => settings.Connection("EventsBroker"), broker);
        }
    }

    // 宿主自身需要的共用端口：表面解析与校验、工具契约散列（Capabilities 与 Registry 共用）。
    internal static void RegisterShared(IServiceCollection services)
    {
        services.AddSingleton<SurfaceValidator>();
        services.AddSingleton<ISurfaceReader>(sp => sp.GetRequiredService<SurfaceValidator>());
        services.AddSingleton<ISurfaceValidator>(sp => sp.GetRequiredService<SurfaceValidator>());
        services.AddSingleton<IToolContractHasher, ToolContractHasher>();
    }
}
