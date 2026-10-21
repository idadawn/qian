using System.Text.Json;
using Kun.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Lifecycle;

// Mount / Unmount 写租户 scope patch（取代数据库开关）并立即合入；装配失败整体撤回，原因持久记入诊断（R76）。
internal sealed partial class LifecycleController
{
    public ValueTask<MountResult> MountAsync(ScopeKey scope, JsonElement entry, string actor, CancellationToken cancellationToken = default)
        => MountCoreAsync(scope, entry, null, actor, cancellationToken);

    public ValueTask<MountResult> MountAtRevisionAsync(ScopeKey scope, JsonElement entry, int expectedRevision, string actor, CancellationToken cancellationToken)
        => MountCoreAsync(scope, entry, expectedRevision, actor, cancellationToken);

    private async ValueTask<MountResult> MountCoreAsync(ScopeKey scope, JsonElement entry, int? expectedRevision, string actor, CancellationToken cancellationToken)
    {
        var tenantId = TenantScope(scope); CheckActor(actor);
        if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || id.GetString() is not { Length: > 0 } entryId)
            throw LifecycleArtifact.Invalid("registry/scope-patches.json#/0/id", "挂载条目必须是带 id 的 patch 对象。");
        using var current = tenant.BeginScope(tenantId);
        using var origin = Origin();
        var registry = context.Registry();
        string? plugin = null;
        if (entry.TryGetProperty("plugin", out var reference))
        {
            plugin = reference.ValueKind == JsonValueKind.String ? reference.GetString() : null;
            if (plugin?.Split('@', 2) is not [var pluginId, var version]) throw LifecycleArtifact.Invalid("registry/scope-patches.json#/0/plugin", "plugin 必须写成 组件@版本。");
            await RequireInstalled(registry, pluginId, version, cancellationToken).ConfigureAwait(false);
        }
        var previous = await registry.GetScopePatchAsync(scope, entryId, cancellationToken).ConfigureAwait(false);
        var saved = await registry.MountAsync(scope, entry, expectedRevision ?? previous?.Revision ?? 0, actor, cancellationToken).ConfigureAwait(false);
        var failure = await Synchronize(registry, scope, cancellationToken).ConfigureAwait(false);
        if (failure is null)
        {
            await ClearFailures(scope, [entryId], cancellationToken).ConfigureAwait(false);
            return new MountResult(Name(scope), entryId, saved.Revision, MountStates.Ready, null);
        }
        // 撤回本次写入：恢复挂载前的 patch（没有则撤销），本 scope 其余条目随即恢复；Registry 的运行时诊断随之清空，所以原因记在这里。
        await registry.RestoreScopePatchAsync(scope, entryId, previous?.Patch, saved.Revision, actor, cancellationToken).ConfigureAwait(false);
        await Synchronize(registry, scope, cancellationToken).ConfigureAwait(false);
        await RecordFailure(scope, failure.EntryId ?? entryId, failure.Plugin ?? plugin, failure.Revision ?? saved.Revision, failure.ReasonCode, cancellationToken).ConfigureAwait(false);
        return new MountResult(Name(scope), entryId, saved.Revision, MountStates.Failed, failure.ReasonCode);
    }

    public ValueTask<MountResult> UnmountAsync(ScopeKey scope, string entryId, string actor, CancellationToken cancellationToken = default)
        => UnmountCoreAsync(scope, entryId, null, actor, cancellationToken);

    public ValueTask<MountResult> UnmountAtRevisionAsync(ScopeKey scope, string entryId, int expectedRevision, string actor, CancellationToken cancellationToken)
        => UnmountCoreAsync(scope, entryId, expectedRevision, actor, cancellationToken);

    private async ValueTask<MountResult> UnmountCoreAsync(ScopeKey scope, string entryId, int? expectedRevision, string actor, CancellationToken cancellationToken)
    {
        var tenantId = TenantScope(scope); CheckActor(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryId);
        using var current = tenant.BeginScope(tenantId);
        using var origin = Origin();
        var registry = context.Registry();
        var patch = await registry.GetScopePatchAsync(scope, entryId, cancellationToken).ConfigureAwait(false);
        if (expectedRevision is { } expected && patch?.Patch.TryGetProperty("disabled", out var disabled) == true && disabled.ValueKind == JsonValueKind.True)
        {
            if (expected != patch.Revision) throw new QianErrorException(new QianError(RegistryErrors.RevisionConflict));
            return new MountResult(Name(scope), entryId, patch.Revision, MountStates.Unmounted, null);
        }
        var revision = await registry.UnmountAsync(scope, entryId, expectedRevision ?? patch?.Revision ?? 0, actor, cancellationToken).ConfigureAwait(false);
        // 卸载已提交；合入失败只可能来自其它条目，原因由诊断给出，不改变卸载结论。
        await Synchronize(registry, scope, cancellationToken).ConfigureAwait(false);
        await ClearFailures(scope, [entryId], cancellationToken).ConfigureAwait(false);
        return new MountResult(Name(scope), entryId, revision, MountStates.Unmounted, null);
    }

    public async ValueTask<IReadOnlyList<LifecycleDiagnosticView>> GetDiagnosticsAsync(ScopeKey scope, ICurrentUser user, CancellationToken cancellationToken = default)
    {
        var tenantId = await AuthorizeAsync(scope, user, cancellationToken).ConfigureAwait(false);
        var current = await context.Registry().GetScopeDiagnosticsAsync(scope, cancellationToken).ConfigureAwait(false);
        await using var session = await context.Data().OpenAsync<LifecycleDbContext>(cancellationToken).ConfigureAwait(false);
        var name = Name(scope);
        var recorded = await session.Context.MountFailures.AsNoTracking().Where(row => row.Scope == name).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (tenant.TenantId != tenantId) throw NotFound();
        return current.Select(item => new LifecycleDiagnosticView(item.Scope, item.EntryId, item.Plugin, item.Revision, item.ReasonCode, item.OccurredAt))
            .Concat(recorded.OrderBy(row => row.EntryId, StringComparer.Ordinal).Select(row => row.View())).ToArray();
    }

    // 合入当前租户 scope；失败时取 Registry 记下的条目、组件修订与原因码（装配异常的 Data 带同样的值），取消照常抛出。
    private async Task<RegistryScopeDiagnostic?> Synchronize(IRegistryService registry, ScopeKey scope, CancellationToken ct)
    {
        try
        {
            await registry.SynchronizeAsync(scope, ct).ConfigureAwait(false);
            return null;
        }
        catch (Exception error) when (error is QianErrorException or InvalidOperationException && error is not ObjectDisposedException && !ct.IsCancellationRequested)
        {
            if (error.Data["reasonCode"] is string code)
                return new RegistryScopeDiagnostic(Name(scope), error.Data["entryId"] as string, error.Data["plugin"] as string, error.Data["revision"] as int?, code, Now());
            var recorded = await registry.GetScopeDiagnosticsAsync(scope, ct).ConfigureAwait(false);
            return recorded.Count > 0 ? recorded[0] : new RegistryScopeDiagnostic(Name(scope), null, null, null,
                error is QianErrorException qian ? qian.Error.Code.Code : PlatformErrors.Internal.Code, Now());
        }
    }

    private async Task RecordFailure(ScopeKey scope, string entryId, string? plugin, int? revision, string reasonCode, CancellationToken ct)
    {
        await using var session = await context.Data().OpenAsync<LifecycleDbContext>(ct).ConfigureAwait(false);
        var name = Name(scope);
        var row = await session.Context.MountFailures.SingleOrDefaultAsync(item => item.Scope == name && item.EntryId == entryId, ct).ConfigureAwait(false);
        if (row is null) session.Context.MountFailures.Add(row = new MountFailureRow { TenantId = scope.Id, Scope = name, EntryId = entryId });
        row.Plugin = plugin is { Length: <= 330 } ? plugin : null; row.Revision = revision; row.ReasonCode = reasonCode; row.OccurredAt = Now();
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task ClearFailures(ScopeKey scope, List<string> entries, CancellationToken ct)
    {
        if (entries.Count == 0) return;
        await using var session = await context.Data().OpenAsync<LifecycleDbContext>(ct).ConfigureAwait(false);
        var name = Name(scope);
        var rows = await session.Context.MountFailures.Where(row => row.Scope == name && entries.Contains(row.EntryId)).ToListAsync(ct).ConfigureAwait(false);
        if (rows.Count == 0) return;
        session.Context.MountFailures.RemoveRange(rows);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // 带 plugin 的条目：组件须已开通到同一版本；宿主预装清单声明 policy.required 的平台组件不经开通。
    private async Task RequireInstalled(IRegistryService registry, string pluginId, string version, CancellationToken ct)
    {
        if (registry.GetInstalledComponents().Any(component => component.PluginId == pluginId && component.Version == version &&
            component.Manifest.TryGetProperty("policy", out var policy) && policy.ValueKind == JsonValueKind.Object &&
            policy.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.True)) return;
        await using var session = await context.Data().OpenAsync<LifecycleDbContext>(ct).ConfigureAwait(false);
        var install = await session.Context.Installs.AsNoTracking().SingleOrDefaultAsync(item => item.PluginId == pluginId, ct).ConfigureAwait(false)
            ?? throw new QianErrorException(new QianError(LifecycleErrors.NotEntitled));
        if (install.State != TenantInstallStates.Installed || install.Version != version) throw Conflict("组件未开通到该版本，请先开通或升级。");
    }

    // 挂载只接受租户 scope（小写规范 UUID）；应用 / 会话 scope 待 Tenancy 的归属证明。
    private static string TenantScope(ScopeKey scope)
    {
        if (scope.Kind != "tenant") throw LifecycleArtifact.Invalid("#/scope", "挂载只接受 tenant scope。");
        CheckTenant(scope.Id);
        return scope.Id;
    }

    // 管理面只认宿主已验证的主体与租户上下文；委托、Agent、跨租户、缺判定端口或无权限一律与不存在同为 NOT_FOUND（R32）。
    private async ValueTask<string> AuthorizeAsync(ScopeKey scope, ICurrentUser user, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);
        var principal = user.Principal;
        if (!user.IsAuthenticated || principal is null || user.UserId != principal.Subject || !tenant.IsMultiTenant || string.IsNullOrWhiteSpace(tenant.TenantId))
            throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        var tenantId = tenant.TenantId;
        if (principal.Actor is not null || principal.Kind is not (PrincipalKind.User or PrincipalKind.Service) || principal.TenantId != tenantId ||
            scope.Kind != "tenant" || scope.Id != tenantId) throw NotFound();
        // 判定端口由宿主的 Authz Provider 装配；缺失时 fail-closed，不回退到令牌角色。
        if (context.Services.GetService<IPermissionChecker>() is not { } checker) throw NotFound();
        var subject = new AuthorizationSubject(principal.Subject, tenantId, principal.Kind == PrincipalKind.User ? "user" : "service");
        var decision = await checker.CheckAsync(new PermissionRequest(subject, LifecycleDefinition.DiagnosticsReadPermission), ct).ConfigureAwait(false);
        // 判定之后再确认请求主体与租户上下文没有变化。
        if (!decision.Allow || !user.IsAuthenticated || user.UserId != principal.Subject || user.Principal?.TenantId != tenantId ||
            user.Principal?.Kind != principal.Kind || user.Principal?.Actor is not null || tenant.TenantId != tenantId) throw NotFound();
        return tenantId;
    }
}
