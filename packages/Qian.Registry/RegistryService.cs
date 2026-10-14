using Kun.Authorization;
using Kun.Core.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Registry;

internal sealed class RegistryService : IRegistryService
{
    private readonly RegistryRuntimeOptions _options;
    private readonly IDataService _data;
    private readonly ITenantContext _tenant;
    private readonly IPluginContext _context;
    private readonly RegistryScopeReconciler _scopes;
    private readonly IReadOnlyList<InstalledComponentMetadata> _installed;
    private readonly IReadOnlySet<string> _installedEntries;
    private readonly IReadOnlyList<string> _serviceKeys;
    private readonly PostgresArtifactCatalog _artifacts;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _control = new(1, 1);
    private PostgresPatchListener? _listener;
    private bool _disposed;

    internal RegistryService(RegistryRuntimeOptions options, IDataService data, ITenantContext tenant, IPluginContext context)
    {
        _options = options; _data = data; _tenant = tenant; _context = context;
        var installed = new Dictionary<(string Id, string Version), InstalledComponentMetadata>();
        foreach (var binding in options.InstalledComponents)
        {
            var metadata = binding.Metadata;
            if (installed.TryGetValue((metadata.PluginId, metadata.Version), out var prior) &&
                (!System.Text.Json.JsonElement.DeepEquals(prior.Manifest, metadata.Manifest) || prior.Metadata.Count != metadata.Metadata.Count ||
                 prior.Metadata.Any(item => !metadata.Metadata.TryGetValue(item.Key, out var value) || !System.Text.Json.JsonElement.DeepEquals(item.Value, value))))
                throw new ArgumentException("同一预装组件版本的元数据不一致。", nameof(options));
            installed[(metadata.PluginId, metadata.Version)] = metadata;
        }
        _installed = Array.AsReadOnly(installed.Values.OrderBy(item => item.PluginId, StringComparer.Ordinal).ThenBy(item => item.Version, StringComparer.Ordinal).ToArray());
        _installedEntries = options.InstalledComponents.Select(binding => binding.Definition.Entry.Id).ToHashSet(StringComparer.Ordinal);
        // 候选制品的 inject 只能由本宿主预装组件提供（PRD 7.2.1）；provides 与 profile 装配器同样按服务键原文匹配。
        _serviceKeys = Array.AsReadOnly(_installed.SelectMany(component => component.Manifest.GetProperty("provides").EnumerateArray().Select(key => key.GetString()!))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        _artifacts = new PostgresArtifactCatalog(data);
        var repository = new PostgresPatchRepository(data, tenant, options.Surfaces);
        var composer = new RegistryProfileComposer(repository, options.Profiles, options.Profile);
        _scopes = new RegistryScopeReconciler(tenant, composer, options.Runtime, context, ResolveInstalled);
    }

    private PluginDefinition ResolveInstalled(Qian.Profiles.Abstractions.ResolvedProfileEntry entry)
    {
        var identity = entry.Plugin.Split('@', 2);
        var installed = _installed.SingleOrDefault(component => component.PluginId == identity[0] && component.Version == identity[1]);
        if (installed is null || !System.Text.Json.JsonElement.DeepEquals(installed.Manifest, entry.Manifest))
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid)
            {
                Errors = [new QianFieldError(RegistryErrors.Invalid, "registry/scope-patches.json#/plugin",
                    new Dictionary<string, string> { ["reason"] = "组件不在宿主已校验的预装集合中，或清单已变化。" })]
            });
        return _options.Resolve(entry);
    }

    public IReadOnlyList<InstalledComponentMetadata> GetInstalledComponents()
    {
        if (_disposed || _lifetime.IsCancellationRequested) throw new ServiceNotReadyException();
        return _installed;
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        await _control.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_listener is not null) return;
            if (_options.Runtime.DumpConfig().Any(entry => entry.Scope == ScopeKey.Global && _options.Runtime.GetState(entry.Id, entry.Scope) != PluginState.Ready))
                throw new InvalidOperationException("Registry 只能在初始插件树 Ready 后启动监听。");
            _listener = await PostgresPatchListener.StartAsync(_data, _tenant, _scopes.RefreshAllAsync, _context.Logger, linked.Token).ConfigureAwait(false);
            if (_lifetime.IsCancellationRequested) { await _listener.DisposeAsync().ConfigureAwait(false); _listener = null; linked.Token.ThrowIfCancellationRequested(); }
        }
        finally { _control.Release(); }
    }

    public async ValueTask SynchronizeAsync(ScopeKey scope, CancellationToken cancellationToken = default)
    {
        if (_disposed || _lifetime.IsCancellationRequested || _listener is null) throw new ServiceNotReadyException();
        // 不能因为调用方传了 app/session id 就推断归属；这些种类待 Tenancy 的真实 lineage 接入。
        if (!_tenant.IsMultiTenant || string.IsNullOrWhiteSpace(_tenant.TenantId)) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        if (scope.Kind != "tenant" || scope.Id != _tenant.TenantId) throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        await _scopes.ActivateAsync(_tenant.TenantId, scope, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<RegistryScopeEntry>> GetScopeEntriesAsync(ScopeKey scope, ServiceKey key, CancellationToken cancellationToken = default)
    {
        if (_disposed || _lifetime.IsCancellationRequested || _listener is null) throw new ServiceNotReadyException();
        if (!_tenant.IsMultiTenant || string.IsNullOrWhiteSpace(_tenant.TenantId)) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        if (scope.Kind != "tenant" || scope.Id != _tenant.TenantId) throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Name);
        var entries = await _scopes.ReadAsync(_tenant.TenantId, scope, key, cancellationToken).ConfigureAwait(false);
        return Array.AsReadOnly(entries.Where(entry => _installed.Any(component => component.PluginId == entry.PluginId && component.Version == entry.Version)).ToArray());
    }

    public async ValueTask<IReadOnlyList<RegistryEntryView>> GetScopeViewAsync(ScopeKey scope, ICurrentUser user, CancellationToken cancellationToken = default)
    {
        var tenantId = await AuthorizeAsync(scope, user, cancellationToken).ConfigureAwait(false);
        return await _scopes.ViewAsync(tenantId, scope, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<RegistryEntryView> GetScopeEntryAsync(ScopeKey scope, string entryId, ICurrentUser user, CancellationToken cancellationToken = default)
    {
        var tenantId = await AuthorizeAsync(scope, user, cancellationToken).ConfigureAwait(false);
        // 判定之后再看条目标识，无权限者得到的永远是同一个 404。
        if (string.IsNullOrWhiteSpace(entryId) || entryId.Length > 128 || entryId.Any(char.IsControl)) throw NotFound();
        return await _scopes.InspectAsync(tenantId, scope, entryId, _installedEntries, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<RegistryArtifactEntry> SubmitArtifactAsync(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> files, string digest, string actor, CancellationToken cancellationToken = default)
    {
        if (_disposed || _lifetime.IsCancellationRequested) throw new ServiceNotReadyException();
        ArgumentNullException.ThrowIfNull(files);
        // 候选制品尚不属于本宿主的完整目录，跨组件引用按离线目录给 warning；inject 的 Provider 必须已在预装集合中。
        var content = RegistryArtifactContent.Read(files, digest, _options.Surfaces,
            new SurfaceValidationOptions { Profile = "platform", ExecutionPlane = "server", ServiceKeys = _serviceKeys });
        var saved = await _artifacts.SubmitAsync(content, actor, cancellationToken).ConfigureAwait(false);
        return new RegistryArtifactEntry(saved.Digest, saved.Component.Id, saved.Component.Manifest.GetProperty("version").GetString()!,
            saved.State, saved.SubmittedBy, saved.SubmittedAt);
    }

    // 管理面只认宿主已验证的主体与租户上下文；委托、Agent、跨租户、缺判定端口或无权限一律与不存在同为 NOT_FOUND（R32）。
    private async ValueTask<string> AuthorizeAsync(ScopeKey scope, ICurrentUser user, CancellationToken ct)
    {
        if (_disposed || _lifetime.IsCancellationRequested || _listener is null) throw new ServiceNotReadyException();
        ArgumentNullException.ThrowIfNull(user);
        var principal = user.Principal;
        if (!user.IsAuthenticated || principal is null || user.UserId != principal.Subject || !_tenant.IsMultiTenant || string.IsNullOrWhiteSpace(_tenant.TenantId))
            throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        var tenantId = _tenant.TenantId;
        if (principal.Actor is not null || principal.Kind is not (PrincipalKind.User or PrincipalKind.Service) || principal.TenantId != tenantId ||
            scope.Kind != "tenant" || scope.Id != tenantId) throw NotFound();
        // 判定端口由宿主的 Authz Provider 装配；缺失时 fail-closed，不回退到令牌角色。
        if (_context.Services.GetService<IPermissionChecker>() is not { } checker) throw NotFound();
        var subject = new AuthorizationSubject(principal.Subject, tenantId, principal.Kind == PrincipalKind.User ? "user" : "service");
        var decision = await checker.CheckAsync(new PermissionRequest(subject, RegistryDefinition.ScopeReadPermission), ct).ConfigureAwait(false);
        // 判定之后再确认请求主体与租户上下文没有变化。
        if (!decision.Allow || !user.IsAuthenticated || user.UserId != principal.Subject || user.Principal?.TenantId != tenantId ||
            user.Principal?.Kind != principal.Kind || user.Principal?.Actor is not null || _tenant.TenantId != tenantId) throw NotFound();
        return tenantId;
    }

    private static QianErrorException NotFound() => new(new QianError(PlatformErrors.NotFound));

    internal void Cancel()
    {
        _lifetime.Cancel(); _listener?.Cancel(); _scopes.Cancel();
    }

    internal async ValueTask CancelAndDrainAsync()
    {
        Cancel();
        await _control.WaitAsync().ConfigureAwait(false);
        try { if (_listener is not null) await _listener.DisposeAsync().ConfigureAwait(false); }
        finally { _control.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        Cancel();
        await _control.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;
            if (_listener is not null) await _listener.DisposeAsync().ConfigureAwait(false);
            await _scopes.DisposeAsync().ConfigureAwait(false);
        }
        finally { _control.Release(); }
    }
}
