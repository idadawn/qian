using System.Text.Json;
using Kun.Authorization;
using Kun.Core.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Events;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Registry;

internal sealed partial class RegistryService : IRegistryService
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
    private readonly PostgresPatchRepository _patches;
    private readonly RegistryProfileComposer _composer;
    private readonly RegistryPatchWriter _writer;
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
        _patches = new PostgresPatchRepository(data, tenant, options.Surfaces);
        _composer = new RegistryProfileComposer(_patches, options.Profiles, options.Profile);
        _writer = new RegistryPatchWriter(_patches, _composer);
        _scopes = new RegistryScopeReconciler(tenant, _composer, options.Runtime, context, ResolveInstalled, DeclaresContracts);
    }

    // 预装制品带契约索引（metadata/contracts.json）的组件版本，挂载时必须经平台契约准入（R76）。
    private bool DeclaresContracts(string pluginId, string version) => _installed.Any(component => component.PluginId == pluginId && component.Version == version &&
        component.Metadata.Keys.Any(path => Path.GetFileName(path) == "contracts.json"));

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

    public async ValueTask<IReadOnlyList<RegistryEntryView>> GetConfigurationViewAsync(ScopeKey scope, CancellationToken cancellationToken = default)
    {
        if (_listener is null) throw new ServiceNotReadyException();
        WriteTenant(scope);
        return await _scopes.ViewAsync(_tenant.TenantId!, scope, cancellationToken).ConfigureAwait(false);
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

    public async ValueTask<ScopePatchSnapshot> MountAsync(ScopeKey scope, JsonElement entry, int expectedRevision, string actor, CancellationToken cancellationToken = default)
    {
        WriteTenant(scope);
        if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String)
            throw Invalid("#/0/id", "挂载条目必须是带 id 的 patch 对象。");
        if (entry.TryGetProperty("disabled", out var disabled) && disabled.ValueKind == JsonValueKind.True)
            throw Invalid("#/0/disabled", "挂载条目不能带 disabled: true；卸载请用 Unmount。");
        CheckInstalled(entry);
        using var origin = Origin();
        var saved = await _writer.PutAsync(scope, id.GetString()!, entry, expectedRevision, actor, true, cancellationToken).ConfigureAwait(false);
        var source = _context.Scoped(scope);
        await source.DispatchAsync(SurfaceHookEvents.EntryMounted, new HookNotification(source, saved.EntryId,
            JsonSerializer.SerializeToElement(new { entryId = saved.EntryId, revision = saved.Revision }), cancellationToken), cancellationToken).ConfigureAwait(false);
        return saved;
    }

    public async ValueTask<int> UnmountAsync(ScopeKey scope, string entryId, int expectedRevision, string actor, CancellationToken cancellationToken = default)
    {
        WriteTenant(scope);
        var current = await _patches.GetAsync(scope, entryId, cancellationToken).ConfigureAwait(false);
        using var origin = Origin();
        if (ProfileEntries(scope).Contains(entryId))
        {
            // 平台条目在本 scope 写禁用 patch（取代数据库开关）；已禁用即本 scope 看不到，与不存在相同。
            if (current is not null && current.Patch.TryGetProperty("disabled", out var disabled) && disabled.ValueKind == JsonValueKind.True) throw NotFound();
            var patch = JsonSerializer.SerializeToElement(new Dictionary<string, object> { ["id"] = entryId, ["disabled"] = true });
            return (await _writer.PutAsync(scope, entryId, patch, expectedRevision, actor, true, cancellationToken).ConfigureAwait(false)).Revision;
        }
        if (current is null)
        {
            if (await _patches.DeletedRevisionAsync(scope, entryId, cancellationToken).ConfigureAwait(false) is not { } removed) throw NotFound();
            if (removed != expectedRevision) throw new QianErrorException(new QianError(RegistryErrors.RevisionConflict));
            return removed;
        }
        return await _writer.RemoveAsync(scope, entryId, expectedRevision, actor, true, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<ScopePatchSnapshot?> GetScopePatchAsync(ScopeKey scope, string entryId, CancellationToken cancellationToken = default)
    {
        WriteTenant(scope);
        return await _patches.GetAsync(scope, entryId, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<ScopePatchSnapshot>> GetScopePatchesAsync(ScopeKey scope, CancellationToken cancellationToken = default)
    {
        WriteTenant(scope);
        return await _patches.ListAsync(scope, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<int> RestoreScopePatchAsync(ScopeKey scope, string entryId, JsonElement? previous, int expectedRevision, string actor, CancellationToken cancellationToken = default)
    {
        WriteTenant(scope);
        using var origin = Origin();
        if (previous is not { } patch) return await _writer.RemoveAsync(scope, entryId, expectedRevision, actor, true, cancellationToken).ConfigureAwait(false);
        if (patch.ValueKind != JsonValueKind.Object || !patch.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || id.GetString() != entryId)
            throw Invalid("#/0/id", "恢复的 patch 必须是同一条目。");
        CheckInstalled(patch);
        return (await _writer.PutAsync(scope, entryId, patch, expectedRevision, actor, true, cancellationToken).ConfigureAwait(false)).Revision;
    }

    public async ValueTask<RegistryArtifactEntry> RecordArtifactStateAsync(string digest, string state, string signedBy, CancellationToken cancellationToken = default)
    {
        if (_disposed || _lifetime.IsCancellationRequested) throw new ServiceNotReadyException();
        var saved = await _artifacts.RecordAsync(digest, state, signedBy, cancellationToken).ConfigureAwait(false);
        return new RegistryArtifactEntry(saved.Digest, saved.Component.Id, saved.Component.Manifest.GetProperty("version").GetString()!,
            saved.State, saved.SubmittedBy, saved.SubmittedAt);
    }

    public async ValueTask<IReadOnlyList<RegistryScopeDiagnostic>> GetScopeDiagnosticsAsync(ScopeKey scope, CancellationToken cancellationToken = default)
    {
        if (_listener is null) throw new ServiceNotReadyException();
        WriteTenant(scope);
        return await _scopes.DiagnoseAsync(_tenant.TenantId!, scope, cancellationToken).ConfigureAwait(false);
    }

    // 新增条目必须写明宿主预装的组件版本；只替换平台条目配置的 patch 不带 plugin。
    private void CheckInstalled(JsonElement entry)
    {
        if (!entry.TryGetProperty("plugin", out var plugin)) return;
        var reference = plugin.ValueKind == JsonValueKind.String ? plugin.GetString()!.Split('@', 2) : [];
        if (reference.Length != 2 || !_installed.Any(component => component.PluginId == reference[0] && component.Version == reference[1]))
            throw Invalid("#/0/plugin", "挂载条目须写明宿主预装集合中的组件版本（plugin@version）。");
    }

    // 公开写入只接受当前已验证租户自己的 tenant scope；应用 / 会话 scope 待 Tenancy 的真实归属证明。
    private void WriteTenant(ScopeKey scope)
    {
        if (_disposed || _lifetime.IsCancellationRequested) throw new ServiceNotReadyException();
        if (!_tenant.IsMultiTenant || string.IsNullOrWhiteSpace(_tenant.TenantId)) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        if (scope.Kind != "tenant" || scope.Id != _tenant.TenantId) throw NotFound();
    }

    // 只看文件层（profile、组合包与 patch 文件）装配出的平台条目，不含任何数据库 scope patch。
    private HashSet<string> ProfileEntries(ScopeKey scope)
    {
        var result = _composer.Compose(scope, []);
        if (!result.IsValid) throw new InvalidOperationException("平台 profile 无法装配。");
        return result.Snapshot!.Entries.Where(entry => entry.Scope == "global").Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
    }

    // 集成事件来源：producer 为本组件；actor 与关联沿用调用方（如代表运营者的 Lifecycle）打开的事件作用域，没有时以本组件服务主体发布。
    private static IDisposable Origin()
    {
        var outer = EventEnvelopeScope.Current;
        return EventEnvelopeScope.Begin(new EventOrigin("qian.registry", outer?.Actor ?? EventActor.Service("qian.registry"))
        { CorrelationId = outer?.CorrelationId, CausationId = outer?.CausationId, TraceParent = outer?.TraceParent });
    }

    private static QianErrorException Invalid(string pointer, string reason) => new(new QianError(PlatformErrors.InputInvalid)
    { Errors = [new QianFieldError(RegistryErrors.Invalid, "registry/scope-patches.json" + pointer, new Dictionary<string, string> { ["reason"] = reason })] });

    // 管理面只认宿主已验证的主体与租户上下文；委托、Agent、跨租户、缺判定端口或无权限一律与不存在同为 NOT_FOUND（R32）。
    private async ValueTask<string> AuthorizeAsync(ScopeKey scope, ICurrentUser user, CancellationToken ct, string permission = RegistryDefinition.ScopeReadPermission)
    {
        if (_disposed || _lifetime.IsCancellationRequested || _listener is null) throw new ServiceNotReadyException();
        ArgumentNullException.ThrowIfNull(user);
        var principal = user.Principal;
        if (!user.IsAuthenticated || principal is null || user.UserId != principal.Subject || !_tenant.IsMultiTenant || string.IsNullOrWhiteSpace(_tenant.TenantId))
            throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        var tenantId = _tenant.TenantId;
        if (principal.Actor is null && principal.Kind == PrincipalKind.User && principal.TenantId is null && scope.Kind == "tenant" && scope.Id == tenantId)
            return await BypassAsync(scope, user, principal, tenantId, permission, ct).ConfigureAwait(false);
        if (principal.Actor is not null || principal.Kind is not (PrincipalKind.User or PrincipalKind.Service) || principal.TenantId != tenantId ||
            scope.Kind != "tenant" || scope.Id != tenantId) throw NotFound();
        // 判定端口由宿主的 Authz Provider 装配；缺失时 fail-closed，不回退到令牌角色。
        if (_context.Services.GetService<IPermissionChecker>() is not { } checker) throw NotFound();
        var subject = new AuthorizationSubject(principal.Subject, tenantId, principal.Kind == PrincipalKind.User ? "user" : "service");
        var decision = await checker.CheckAsync(new PermissionRequest(subject, permission), ct).ConfigureAwait(false);
        // 判定之后再确认请求主体与租户上下文没有变化。
        if (!decision.Allow || !user.IsAuthenticated || user.UserId != principal.Subject || user.Principal?.TenantId != tenantId ||
            user.Principal?.Kind != principal.Kind || user.Principal?.Actor is not null || _tenant.TenantId != tenantId) throw NotFound();
        return tenantId;
    }

    // 平台主体（无租户）读取目标租户的 scope 视图：调用方已把租户上下文切到目标租户，判定只走 Authz 的超管显式旁路（ADR-006 D6.3），
    // 由旁路写入目标租户审计链（bypass = superadmin）。旁路端口未装配、非超管或审计失败时与不存在同为 404。
    private async ValueTask<string> BypassAsync(ScopeKey scope, ICurrentUser user, Principal principal, string tenantId, string permission, CancellationToken ct)
    {
        if (_context.TryInject<ISuperadminBypass>(AuthzDefinition.SuperadminBypassKey) is not { } bypass) throw NotFound();
        var decision = await bypass.AuthorizeAsync(new SuperadminAccess(new AuthorizationSubject(principal.Subject, null, "user"),
            permission, tenantId, "registry/scope:" + scope.Kind + ":" + scope.Id, "api"), ct).ConfigureAwait(false);
        if (!decision.Allow || !user.IsAuthenticated || user.UserId != principal.Subject || user.Principal?.TenantId is not null ||
            user.Principal?.Kind != PrincipalKind.User || user.Principal?.Actor is not null || _tenant.TenantId != tenantId) throw NotFound();
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
