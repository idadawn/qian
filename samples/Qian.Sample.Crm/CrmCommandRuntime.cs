using System.Text.Json;
using Kun.Core.MultiTenancy;
using Qian.Audit;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Data;
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
        var registry = services.GetRequiredService<IPermissionRegistry>();
        var catalog = CrmPermissions.Compile();
        _permissions = registry.Register("1.0.0", catalog.Permissions);
        IReadOnlyList<PluginDefinition> providers = [
            Define("data", "qian.data", new DataPlugin(), [DataDefinition.Key]),
            Define("tenancy", "qian.tenancy", new TenancyPlugin(), [TenancyDefinition.Key, CommandPolicies.TenancyKey]),
            Define("authz", "qian.authz", new RbacCommandPolicyPlugin(), [CommandPolicies.AuthorizationKey]),
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
            var context = _loader.GetContext("crm", ScopeKey.Global).Scoped(new ScopeKey("tenant", tenant.TenantId!));
            _ = context.Inject<ICrmCommands>(CrmDefinition.Key); return context;
        }
        catch (Exception error) when (error is ServiceNotReadyException or KeyNotFoundException or ObjectDisposedException or InvalidOperationException)
        { throw new QianErrorException(new QianError(PlatformErrors.NotFound)); }
    }
    public Task StopAsync(CancellationToken cancellationToken) => DisposeAsync().AsTask();
    public async ValueTask DisposeAsync()
    {
        await _loader.DisposeAsync().ConfigureAwait(false); Interlocked.Exchange(ref _permissions, null)?.Dispose();
    }
    private static PluginDefinition Define(string id, string component, IQianPlugin plugin, IReadOnlyList<ServiceKey> provides) => new(
        new PluginEntry(id, component, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), plugin, provides);
}
