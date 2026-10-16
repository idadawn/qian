using System.Text.Json;
using Kun.Core.MultiTenancy;
using Qian.Audit;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Events;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Tenancy;

namespace Qian.Sample.Crm;

/// <summary>参考宿主的实际插件树；不宣称完整 platform 或 Identity 服务已部署。</summary>
public sealed class CrmCommandRuntime(IServiceProvider services, ITenantContext tenant) : IHostedService, IAsyncDisposable
{
    private readonly PluginLoader _loader = new(services);
    private IDisposable? _permissions;
    internal PluginLoader Loader => _loader;
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // 端点的权限判定器（RequireKunPermission）读宿主的同一份活动登记表。宿主自己也持有一份同内容登记（与 Provider 共享租约），
        // authz 组件卸载后业务端点仍按依赖缺失返回 404，而不是因声明撤销先在端点判定处报 403（T23 的 dependency-removed）。
        var catalog = CrmPermissions.Compile();
        _permissions = services.GetRequiredService<IPermissionRegistry>().Register(catalog.Version, catalog.Permissions);
        IReadOnlyList<PluginDefinition> providers = [
            Define("data", "qian.data", new DataPlugin(), [DataDefinition.Key]),
            Define("events", "qian.events", new EventsPlugin(), [EventsDefinition.Key]),
            Define("tenancy", "qian.tenancy", new TenancyPlugin(), [TenancyDefinition.Key, CommandPolicies.TenancyKey], TenancyConfiguration(services.GetRequiredService<IConfiguration>())),
            // 完整 Authz Provider：持久化并登记本应用 metadata 编译出的权限目录、注册命令 PEP、提供 ctx.Authz，并监听 tenant/provisioned 实例化默认角色。
            // 声明的 provides 只列 plugin.schema.json 服务键模式接受的两个键；Authz.Permissions 等其余端口由 Provider 额外提供，不进清单。
            Define("authz", "qian.authz", new AuthzPlugin([catalog]), [CommandPolicies.AuthorizationKey, AuthzDefinition.Key]),
            Define("audit", "qian.audit", new AuditPlugin(), [AuditDefinition.Key, CommandPolicies.AuditKey]),
            Define("crm", "qian.sample.crm", new CrmPlugin(), [CrmDefinition.Key])];
        var assembly = services.GetService<ICrmRuntimeAssembly>();
        if (assembly is not null) providers = await assembly.ComposeAsync(_loader, providers, cancellationToken).ConfigureAwait(false);
        await _loader.MountAsync(providers, cancellationToken).ConfigureAwait(false);
        foreach (var definition in providers)
            if (_loader.GetState(definition.Entry.Id, definition.Entry.Scope) != PluginState.Ready)
                throw new InvalidOperationException("参考宿主必要组件未就绪：" + definition.Entry.Id, _loader.GetFailure(definition.Entry.Id, definition.Entry.Scope));
        if (assembly is not null) await assembly.ReadyAsync(_loader, cancellationToken).ConfigureAwait(false);
    }
    public IPluginContext Source()
    {
        if (!Guid.TryParseExact(tenant.TenantId, "D", out var id) || id == Guid.Empty) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        try
        {
            // 平台宿主经 Lifecycle 把组件挂进租户 scope 时优先用该租户自己的条目（版本可能与全局不同），否则用全局条目。
            var scope = new ScopeKey("tenant", tenant.TenantId!);
            var context = _loader.DumpConfig().Any(entry => entry.Id == "crm" && entry.Scope == scope)
                ? _loader.GetContext("crm", scope) : _loader.GetContext("crm", ScopeKey.Global).Scoped(scope);
            _ = context.Inject<ICrmCommands>(CrmDefinition.Key); return context;
        }
        catch (Exception error) when (error is ServiceNotReadyException or KeyNotFoundException or ObjectDisposedException or InvalidOperationException)
        { throw new QianErrorException(new QianError(PlatformErrors.NotFound)); }
    }
    // 当前租户视角的 authz 组件上下文（me/permissions 用）；授权组件未挂载或已卸载时与不存在同为 404。
    internal IPluginContext AuthzSource()
    {
        if (!Guid.TryParseExact(tenant.TenantId, "D", out var id) || id == Guid.Empty) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        try { return _loader.GetContext("authz", ScopeKey.Global).Scoped(new ScopeKey("tenant", tenant.TenantId!)); }
        catch (Exception error) when (error is KeyNotFoundException or ObjectDisposedException or InvalidOperationException)
        { throw new QianErrorException(new QianError(PlatformErrors.NotFound)); }
    }
    public Task StopAsync(CancellationToken cancellationToken) => DisposeAsync().AsTask();
    public async ValueTask DisposeAsync()
    {
        await _loader.DisposeAsync().ConfigureAwait(false); Interlocked.Exchange(ref _permissions, null)?.Dispose();
    }
    internal static JsonElement TenancyConfiguration(IConfiguration configuration) => JsonSerializer.SerializeToElement(new
    {
        resolver = new { mode = configuration["Tenancy:Resolver:Mode"] ?? "production", allowHeaderFallback = configuration.GetValue<bool>("Tenancy:Resolver:AllowHeaderFallback") },
        initializationCapabilities = configuration.GetSection("Tenancy:InitializationCapabilities").Get<string[]>() ?? []
    });
    private static PluginDefinition Define(string id, string component, IQianPlugin plugin, IReadOnlyList<ServiceKey> provides, object? config = null) => new(
        new PluginEntry(id, component, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(config ?? new { })), plugin, provides);
}
