using System.Security.Cryptography;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage;
using Qian.Audit;
using Qian.Authz.Abstractions;
using Npgsql;
using System.Text;
using Kun.Authorization;
using Microsoft.EntityFrameworkCore;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Tenancy;

namespace Qian.Capabilities;

internal sealed class NavigationService(IRegistryService registry, IPermissionChecker permissions, IDataService data) : INavigationService, IDisposable
{
    private readonly (CancellationTokenSource Source, CancellationToken Token) _lifetime = CreateLifetime();
    private int _disposed;
    public async ValueTask<IReadOnlyList<NavigationItem>> ListAsync(IPluginContext caller, ICurrentUser user, CancellationToken cancellationToken = default)
    {
        using var linked = Link(cancellationToken); var (actor, visible) = await Visible(caller, user, linked.Token).ConfigureAwait(false);
        await using var session = await data.OpenAsync<CapabilitiesDbContext>(linked.Token).ConfigureAwait(false);
        var subjectKey = SubjectKey(actor);
        var preferences = await session.Context.NavigationPreferences.AsNoTracking().Where(row => row.SubjectKey == subjectKey && row.SubjectType == actor.Kind && row.SubjectId == actor.Id)
            .ToDictionaryAsync(row => row.ResourceRef, StringComparer.Ordinal, linked.Token).ConfigureAwait(false);
        Check(user, actor);
        return visible.Where(item => !preferences.TryGetValue(item.ResourceRef, out var pref) || !pref.Hidden)
            .Select(item => preferences.TryGetValue(item.ResourceRef, out var pref) && pref.Order is { } order ? item with { Order = order } : item)
            .OrderBy(item => item.Order).ThenBy(item => item.ResourceRef, StringComparer.Ordinal).ToArray();
    }
    public async ValueTask<NavigationItem> GetAsync(IPluginContext caller, ICurrentUser user, string resourceRef, CancellationToken cancellationToken = default)
    {
        using var linked = Link(cancellationToken); var (_, visible) = await Visible(caller, user, linked.Token).ConfigureAwait(false);
        return visible.SingleOrDefault(item => item.ResourceRef == resourceRef) ?? throw Error(PlatformErrors.NotFound);
    }
    public async ValueTask SetPreferenceAsync(IPluginContext caller, ICurrentUser user, string resourceRef, NavigationPreference preference, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { await SetPreference(caller, user, resourceRef, preference, cancellationToken).ConfigureAwait(false); return; }
            catch (Exception error) when (attempt < 5 && Retryable(error))
            { await Task.Delay(10 * (attempt + 1), cancellationToken).ConfigureAwait(false); }
            catch (Exception error) when (Retryable(error)) { throw Error(PlatformErrors.Conflict); }
        }
    }
    private async Task SetPreference(IPluginContext caller, ICurrentUser user, string resourceRef, NavigationPreference preference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preference);
        if (preference.Order is < -10000 or > 10000) throw Error(PlatformErrors.InputInvalid);
        using var linked = Link(cancellationToken); var (actor, visible) = await Visible(caller, user, linked.Token).ConfigureAwait(false);
        if (!visible.Any(item => item.ResourceRef == resourceRef)) throw Error(PlatformErrors.NotFound);
        var audit = caller.Audit();
        await using var session = await data.OpenAsync<CapabilitiesDbContext>(linked.Token).ConfigureAwait(false);
        if (session.Context.TenantIdForModelCacheKey != actor.TenantId) throw Error(PlatformErrors.NotFound);
        await using var transaction = await session.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable, linked.Token).ConfigureAwait(false);
        var subjectKey = SubjectKey(actor); var resourceKey = Hash(resourceRef); var key = "navigation:" + actor.TenantId + ":" + subjectKey;
        await session.Context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", linked.Token).ConfigureAwait(false);
        var row = await session.Context.NavigationPreferences.SingleOrDefaultAsync(item => item.SubjectKey == subjectKey && item.ResourceKey == resourceKey, linked.Token).ConfigureAwait(false);
        if (row is null)
        {
            row = new NavigationPreferenceRow { TenantId = actor.TenantId!, SubjectKey = subjectKey, ResourceKey = resourceKey, SubjectType = actor.Kind, SubjectId = actor.Id, ResourceRef = resourceRef };
            session.Context.NavigationPreferences.Add(row);
        }
        if (row.SubjectType != actor.Kind || row.SubjectId != actor.Id || row.ResourceRef != resourceRef) throw Error(PlatformErrors.Conflict);
        row.Hidden = preference.Hidden; row.Order = preference.Order;
        Check(user, actor); await session.SaveChangesAsync(linked.Token).ConfigureAwait(false);
        // 字段只包含布尔、整数与 ASCII 资源 URN；固定字典序 JSON 即本输入的规范化形式，不记录原始偏好值。
        var input = "{\"hidden\":" + (preference.Hidden ? "true" : "false") + ",\"order\":" + (preference.Order?.ToString(CultureInfo.InvariantCulture) ?? "null") + ",\"resourceRef\":" + JsonSerializer.Serialize(resourceRef) + "}";
        var record = new AuditRecord(Guid.CreateVersion7(), actor.TenantId, actor.Kind, actor.Id, null, "qian.capabilities/navigation.preference", "navigation-v1",
            Hash(input), "api", resourceRef, "success", Activity.Current?.TraceId.ToHexString() ?? ActivityTraceId.CreateRandom().ToHexString(), DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(new { }));
        await audit.AppendEnlistedAsync(record, transaction.GetDbTransaction(), linked.Token).ConfigureAwait(false);
        Check(user, actor);
        await transaction.CommitAsync(linked.Token).ConfigureAwait(false);
    }
    public async ValueTask<ShellCatalog> GetShellAsync(IPluginContext caller, ICurrentUser user, CancellationToken cancellationToken = default)
    {
        using var linked = Link(cancellationToken); var (actor, tenant) = await Subject(caller, user, linked.Token).ConfigureAwait(false);
        var current = new ShellTenant(actor.TenantId!, tenant?.Name ?? actor.TenantId!);
        if (tenant?.Status is not (TenantStatus.Active or TenantStatus.Trial)) { Check(user, actor); return new ShellCatalog(current, []); }
        // 与菜单同一来源：Registry 的租户 scope 视图（已挂载、Ready、属于已安装制品，已按遮蔽与 restrict 合入）与 Runtime 里实际登记的贡献取交集。
        var view = await registry.GetScopeEntriesAsync(caller.Scope, CapabilitiesDefinition.ShellRemotesKey, linked.Token).ConfigureAwait(false);
        var result = new List<ShellRemote>();
        foreach (var registration in caller.List(CapabilitiesDefinition.ShellRemotesKey))
        {
            // 平台组件（S1-YUE-018）只进平台控制台目录：全局注册在租户 scope 里同样可见，这里显式排除。
            if (registration.Value is not ShellRemoteContribution contribution || contribution.PluginId != registration.Name || contribution.Platform) continue;
            var entry = view.FirstOrDefault(item => item.EntryId == registration.OwnerEntryId && item.Scope == registration.Scope && item.Name == registration.Name && item.PluginId == contribution.PluginId);
            if (entry is null) continue;
            try
            {
                if (!ReferenceEquals(caller.Inject<ShellRemoteContribution>(CapabilitiesDefinition.ShellRemotesKey, contribution.PluginId), contribution)) continue;
            }
            catch (Exception error) when (error is QianErrorException or ObjectDisposedException) { continue; }
            result.Add(new ShellRemote(contribution.PluginId, entry.Version, ScopeText(registration.Scope), contribution.Manifest));
        }
        linked.Token.ThrowIfCancellationRequested(); Check(user, actor);
        return new ShellCatalog(current, result.OrderBy(item => item.Id, StringComparer.Ordinal).ThenBy(item => item.Scope, StringComparer.Ordinal).ToArray());
    }
    public async ValueTask<PlatformConsoleCatalog> GetConsoleAsync(IPluginContext caller, ICurrentUser user, CancellationToken cancellationToken = default)
    {
        using var linked = Link(cancellationToken); var actor = PlatformSubject(caller, user);
        // 判定端口是可选依赖：Authz 未挂载或已卸载时按不持角色处理，不报错、不回落到别的判定。
        var check = caller.TryInject<ISuperadminCheck>(AuthzDefinition.SuperadminCheckKey);
        if (check is null) { Check(user, actor); return new PlatformConsoleCatalog([], [], []); }
        // 与租户菜单同一来源：Registry 的全局视图（已挂载、Ready、属于已安装制品）与 Runtime 里实际登记的贡献取交集，只收平台组件。
        var remotesView = await registry.GetGlobalEntriesAsync(CapabilitiesDefinition.ShellRemotesKey, linked.Token).ConfigureAwait(false);
        var navigationView = await registry.GetGlobalEntriesAsync(CapabilitiesDefinition.NavigationItemsKey, linked.Token).ConfigureAwait(false);
        var candidates = new List<(ShellRemoteContribution Contribution, RegistryScopeEntry Entry)>();
        foreach (var registration in caller.List(CapabilitiesDefinition.ShellRemotesKey))
        {
            if (registration.Value is not ShellRemoteContribution { Platform: true } contribution || contribution.PluginId != registration.Name || registration.Scope != ScopeKey.Global) continue;
            var entry = remotesView.FirstOrDefault(item => item.EntryId == registration.OwnerEntryId && item.Scope == registration.Scope && item.Name == registration.Name && item.PluginId == contribution.PluginId);
            if (entry is null || !Current(caller, CapabilitiesDefinition.ShellRemotesKey, contribution.PluginId, contribution)) continue;
            candidates.Add((contribution, entry));
        }
        var declared = candidates.SelectMany(item => item.Contribution.Permissions).Distinct(StringComparer.Ordinal).ToArray();
        // 只读判定：与超管旁路同一套条件（平台用户主体、权限点已登记、持有效 platform.superadmin），不写审计、不放行任何操作。
        var allowed = declared.Length == 0 ? new HashSet<string>(StringComparer.Ordinal)
            : (await check.AllowedAsync(actor, declared, linked.Token).ConfigureAwait(false)).ToHashSet(StringComparer.Ordinal);
        // 组件清单里至少一个声明可用才把它的远程交给外壳；页面与插槽仍由外壳按下面的权限码逐项过滤。
        var kept = candidates.Where(item => item.Contribution.Permissions.Any(allowed.Contains)).ToArray();
        var plugins = kept.Select(item => item.Contribution.PluginId).ToHashSet(StringComparer.Ordinal);
        var navigation = new List<NavigationItem>();
        foreach (var registration in caller.List(CapabilitiesDefinition.NavigationItemsKey))
        {
            if (registration.Value is not NavigationContribution { Platform: true } contribution || contribution.Name != registration.Name || registration.Scope != ScopeKey.Global ||
                !plugins.Contains(contribution.PluginId) || !allowed.Contains(contribution.Permission) ||
                !navigationView.Any(entry => entry.EntryId == registration.OwnerEntryId && entry.Scope == registration.Scope && entry.Name == registration.Name && entry.PluginId == contribution.PluginId) ||
                !Current(caller, CapabilitiesDefinition.NavigationItemsKey, contribution.Name, contribution)) continue;
            navigation.Add(contribution.Describe("global"));
        }
        var permissions = kept.SelectMany(item => item.Contribution.Permissions).Where(allowed.Contains).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        linked.Token.ThrowIfCancellationRequested(); Check(user, actor);
        return new PlatformConsoleCatalog(
            kept.Select(item => new ShellRemote(item.Contribution.PluginId, item.Entry.Version, "global", item.Contribution.Manifest)).OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            navigation.OrderBy(item => item.Order).ThenBy(item => item.ResourceRef, StringComparer.Ordinal).ToArray(), permissions);
    }
    // 判定期间贡献被撤销或替换（组件卸载、重新 Apply）时不交出旧实例。
    private static bool Current<TService>(IPluginContext caller, ServiceKey key, string name, TService contribution) where TService : class
    {
        try { return ReferenceEquals(caller.Inject<TService>(key, name), contribution); }
        catch (Exception error) when (error is QianErrorException or ObjectDisposedException) { return false; }
    }
    // 平台控制台只给人类平台用户：无租户、无委托、全局上下文；租户主体、服务、Agent 与委托一律按不存在拒绝。
    private AuthorizationSubject PlatformSubject(IPluginContext caller, ICurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(caller); ArgumentNullException.ThrowIfNull(user);
        var principal = user.Principal;
        if (!user.IsAuthenticated || principal is null || user.UserId != principal.Subject || principal.Subject.Length is 0 or > 200) throw Error(PlatformErrors.Unauthenticated);
        if (principal.Kind != PrincipalKind.User || principal.Actor is not null || principal.TenantId is not null || caller.Scope != ScopeKey.Global ||
            !ReferenceEquals(caller.TryInject<IRegistryService>(RegistryDefinition.Key), registry)) throw Error(PlatformErrors.NotFound);
        return new AuthorizationSubject(principal.Subject, null, "user");
    }
    private static string ScopeText(ScopeKey scope) => scope == ScopeKey.Global ? "global" : scope.Kind + ":" + scope.Id;
    private async Task<(AuthorizationSubject Actor, TenantDescriptor? Tenant)> Subject(IPluginContext caller, ICurrentUser user, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller); ArgumentNullException.ThrowIfNull(user);
        var principal = user.Principal;
        if (!user.IsAuthenticated || principal is null || user.UserId != principal.Subject || principal.Subject.Length is 0 or > 200) throw Error(PlatformErrors.Unauthenticated);
        if (principal.Kind is not (PrincipalKind.User or PrincipalKind.Service) || principal.Actor is not null || caller.Scope.Kind != "tenant" ||
            principal.TenantId != caller.Scope.Id || !Guid.TryParseExact(principal.TenantId, "D", out var tenantId) ||
            !ReferenceEquals(caller.TryInject<IRegistryService>(RegistryDefinition.Key), registry)) throw Error(PlatformErrors.NotFound);
        var actor = new AuthorizationSubject(principal.Subject, principal.TenantId, principal.Kind == PrincipalKind.User ? "user" : "service");
        return (actor, await caller.Tenancy().GetByIdAsync(tenantId, ct).ConfigureAwait(false));
    }
    private async Task<(AuthorizationSubject Actor, IReadOnlyList<NavigationItem> Items)> Visible(IPluginContext caller, ICurrentUser user, CancellationToken ct)
    {
        var (actor, tenant) = await Subject(caller, user, ct).ConfigureAwait(false);
        if (tenant?.Status is not (TenantStatus.Active or TenantStatus.Trial)) return (actor, []);
        var view = await registry.GetScopeEntriesAsync(caller.Scope, CapabilitiesDefinition.NavigationItemsKey, ct).ConfigureAwait(false);
        var result = new List<NavigationItem>();
        foreach (var registration in caller.List(CapabilitiesDefinition.NavigationItemsKey))
        {
            if (registration.Value is not NavigationContribution contribution || contribution.Name != registration.Name || contribution.Platform ||
                !view.Any(entry => entry.EntryId == registration.OwnerEntryId && entry.Scope == registration.Scope && entry.Name == registration.Name && entry.PluginId == contribution.PluginId)) continue;
            if (!(await permissions.CheckAsync(new PermissionRequest(actor, contribution.Permission), ct).ConfigureAwait(false)).Allow) continue;
            try
            {
                if (ReferenceEquals(caller.Inject<NavigationContribution>(CapabilitiesDefinition.NavigationItemsKey, contribution.Name), contribution)) result.Add(contribution.Describe(actor.TenantId!));
            }
            catch (Exception error) when (error is QianErrorException or ObjectDisposedException) { }
        }
        ct.ThrowIfCancellationRequested(); Check(user, actor); return (actor, result.AsReadOnly());
    }
    private void Check(ICurrentUser user, AuthorizationSubject actor)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var principal = user.Principal;
        if (!user.IsAuthenticated || principal is null || user.UserId != actor.Id || principal.TenantId != actor.TenantId ||
            (principal.Kind switch { PrincipalKind.User => "user", PrincipalKind.Service => "service", _ => null }) != actor.Kind || principal.Actor is not null) throw Error(PlatformErrors.NotFound);
    }
    private CancellationTokenSource Link(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        { try { _lifetime.Source.Cancel(); } finally { _lifetime.Source.Dispose(); } }
    }
    private static (CancellationTokenSource, CancellationToken) CreateLifetime() { var source = new CancellationTokenSource(); return (source, source.Token); }
    private static bool Retryable(Exception error)
    {
        var current = error;
        while (current is DbUpdateException or InvalidOperationException && current.InnerException is { } inner) current = inner;
        return current is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected } ||
            current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "pk_navigation_preferences" };
    }
    private static string SubjectKey(AuthorizationSubject actor) => Hash(actor.Kind + ":" + actor.Id);
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static QianErrorException Error(ErrorCode code) => new(new QianError(code));
}
