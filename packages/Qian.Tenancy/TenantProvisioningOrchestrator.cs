using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Lifecycle;
using Qian.Events;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Tenancy;

// 每步独立提交；会话级 advisory lock 只串行同一开户，不把外部组件副作用裹进数据库事务。
// 进程退出会释放锁；running 步骤下次用相同幂等键重试。成功步骤永不重放，skipped 步骤可以补跑。
internal sealed class TenantProvisioningOrchestrator(IPluginContext context, PostgresTenantDirectory directory, IReadOnlySet<string> initializationCapabilities) : ITenantProvisioning, IDisposable
{
    private readonly ITenantContext _tenant = context.Services.GetRequiredService<ITenantContext>();
    private int _disposed;
    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
    private void Check()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_tenant.TenantId is not null) throw Error(PlatformErrors.AuthzDenied);
    }

    public async ValueTask<TenantDescriptor> ProvisionAsync(CreateTenant request, TenantAdministrator administrator, string actor, CancellationToken cancellationToken = default)
    {
        Check(); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(administrator);
        PostgresTenantDirectory.ValidateCode(request.Code); PostgresTenantDirectory.ValidateName(request.Name); ValidateActor(actor);
        if (request.Isolation != TenantIsolation.Row || administrator.MembershipId == Guid.Empty || string.IsNullOrWhiteSpace(administrator.SubjectId) || administrator.SubjectId.Length > 200 || administrator.SubjectId.Any(char.IsControl))
            throw Error(PlatformErrors.InputInvalid);
        var input = JsonSerializer.Serialize(new ProvisionInput(request, administrator));
        var id = await Locked("code:" + request.Code, async () =>
        {
            await using var lookup = await context.Data().OpenAsync<TenancyDbContext>(cancellationToken).ConfigureAwait(false);
            var existing = await lookup.Context.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Code == request.Code, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                if (existing.ProvisionInput is null || JsonSerializer.Deserialize<ProvisionInput>(existing.ProvisionInput) != new ProvisionInput(request, administrator))
                    throw Error(PlatformErrors.Conflict);
                return existing.Id;
            }
            var row = new TenantRow { Code = request.Code, Name = request.Name, ProvisionInput = input, CreatedBy = actor };
            using var tenantScope = _tenant.BeginScope(row.Id.ToString());
            await using var session = await context.Data().OpenAsync<TenancyDbContext>(cancellationToken).ConfigureAwait(false);
            session.Context.Tenants.Add(row);
            session.Context.ProvisionSteps.Add(new TenantProvisionStepRow { Step = "01:directory", Status = "succeeded", Attempts = 1 });
            await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return row.Id;
        }, cancellationToken).ConfigureAwait(false);
        return await RetryAsync(id, actor, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<TenantDescriptor> RetryAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        Check(); ValidateActor(actor);
        return await Locked("tenant:" + id, async () =>
        {
            using var tenantScope = _tenant.BeginScope(id.ToString());
            var scope = context.Scoped(new ScopeKey("tenant", id.ToString()));
            TenantRow row;
            await using (var session = await context.Data().OpenAsync<TenancyDbContext>(cancellationToken).ConfigureAwait(false))
                row = await session.Context.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound);
            var input = row.ProvisionInput is null ? throw Error(PlatformErrors.Conflict) : JsonSerializer.Deserialize<ProvisionInput>(row.ProvisionInput)!;
            var descriptor = PostgresTenantDirectory.Describe(row);
            await Step("02:isolation", async () =>
            {
                // Shared 的 Kun 开户器是官方 no-op；RLS 由迁移建立，在线开户只校验，不使用 DDL 角色。
                var provisioner = context.Services.GetService<ITenantProvisioner>() ?? NullTenantProvisioner.Instance;
                await provisioner.ProvisionAsync(new TenantInfo(id.ToString(), row.Name), cancellationToken).ConfigureAwait(false);
                await using var session = await context.Data().OpenAsync<TenancyDbContext>(cancellationToken).ConfigureAwait(false);
                var current = await session.Context.Database.SqlQueryRaw<string>("SELECT current_setting('app.current_tenant_id', true) AS \"Value\"").SingleAsync(cancellationToken).ConfigureAwait(false);
                var ready = await session.Context.Database.SqlQueryRaw<bool>("""
                    SELECT bool_and(c.relrowsecurity AND c.relforcerowsecurity AND EXISTS (SELECT 1 FROM pg_policy p WHERE p.polrelid = c.oid)) AS "Value"
                    FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE n.nspname = 'plugin_qian_tenancy' AND c.relname IN ('tenant_admins', 'tenant_provision_steps')
                    """).SingleAsync(cancellationToken).ConfigureAwait(false);
                if (current != id.ToString() || !ready) throw Error(PlatformErrors.PreconditionFailed);
                return null;
            }).ConfigureAwait(false);
            await Step("03:components", async () =>
            {
                var registry = context.Registry(); var lifecycle = context.Lifecycle();
                var plugins = registry.GetInstalledComponents().Where(component =>
                    component.Manifest.TryGetProperty("policy", out var policy) && policy.TryGetProperty("tier", out var tier) && tier.GetString() == "system")
                    .Select(component => component.PluginId).ToHashSet(StringComparer.Ordinal);
                foreach (var patch in await registry.GetScopePatchesAsync(scope.Scope, cancellationToken).ConfigureAwait(false))
                {
                    if (patch.Patch.TryGetProperty("disabled", out var disabled) && disabled.ValueKind == JsonValueKind.True) continue;
                    if (patch.Patch.TryGetProperty("plugin", out var plugin)) plugins.Add(plugin.GetString()!.Split('@')[0]);
                }
                foreach (var plugin in plugins.Order(StringComparer.Ordinal))
                    await Step("03:component:" + plugin, async () =>
                    {
                        await lifecycle.ProvisionAsync(id.ToString(), plugin, actor, cancellationToken).ConfigureAwait(false);
                        return null;
                    }).ConfigureAwait(false);
                return null;
            }).ConfigureAwait(false);
            await Step("04:provisioned", async () =>
            {
                // 授予端口必须存在，不能把无监听的空分发标为成功；初始授权严格排在角色监听之后。
                _ = scope.Inject<ITenantRoleGrant>(TenantRoleGrant.Key);
                var origin = EventEnvelopeScope.Current;
                if (origin is not null && (origin.Actor.Id != actor || origin.Actor.OnBehalfOf is not null || origin.Actor.Kind == EventActorKind.Agent))
                    throw Error(PlatformErrors.AuthzDenied);
                using var initialization = TenantInitializationAuthorization.Begin(scope, actor, initializationCapabilities,
                    origin?.Actor.Kind == EventActorKind.Service ? "service" : "user");
                await scope.DispatchAsync(SurfaceHookEvents.TenantProvisioned,
                    new HookNotification(scope, id.ToString(), JsonSerializer.SerializeToElement(new { code = row.Code }), cancellationToken), cancellationToken).ConfigureAwait(false);
                return null;
            }).ConfigureAwait(false);
            await Step("05:membership", async () =>
            {
                var membership = scope.TryInject<ITenantMembershipProvisioner>(TenantProvisioning.MembershipKey);
                if (membership is null) return "identity_unavailable";
                await membership.ProvisionAsync(input.Administrator, id + ":membership", cancellationToken).ConfigureAwait(false);
                return null;
            }).ConfigureAwait(false);
            await Step("06:administrator", async () =>
            {
                await using var session = await context.Data().OpenAsync<TenancyDbContext>(cancellationToken).ConfigureAwait(false);
                var admin = await session.Context.Administrators.SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
                if (admin is null) session.Context.Administrators.Add(new TenantAdminRow { MembershipId = input.Administrator.MembershipId, CreatedBy = actor });
                else if (admin.MembershipId != input.Administrator.MembershipId) throw Error(PlatformErrors.Conflict);
                await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }).ConfigureAwait(false);
            await Step("07:owner", async () =>
            {
                await scope.Inject<ITenantRoleGrant>(TenantRoleGrant.Key).AssignAsync("tenant.owner", "user", input.Administrator.SubjectId, actor, cancellationToken).ConfigureAwait(false);
                return null;
            }).ConfigureAwait(false);
            await Step("08:notification", async () =>
            {
                var notifier = scope.TryInject<ITenantProvisionedNotifier>(TenantProvisioning.NotificationKey);
                if (notifier is null) return "notify_unavailable";
                await notifier.NotifyAsync(descriptor, id + ":notification", cancellationToken).ConfigureAwait(false);
                return null;
            }).ConfigureAwait(false);
            return descriptor;

            async Task Step(string name, Func<Task<string?>> work)
            {
                await using (var session = await context.Data().OpenAsync<TenancyDbContext>(cancellationToken).ConfigureAwait(false))
                {
                    var step = await session.Context.ProvisionSteps.SingleOrDefaultAsync(x => x.Step == name, cancellationToken).ConfigureAwait(false);
                    if (step?.Status == "succeeded") return;
                    if (step is null) { step = new TenantProvisionStepRow { Step = name }; session.Context.ProvisionSteps.Add(step); }
                    step.Status = "running"; step.Reason = null; step.Attempts++;
                    await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                try
                {
                    var reason = await work().ConfigureAwait(false);
                    await Finish(reason is null ? "succeeded" : "skipped", reason, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    // 不记录上游消息、SQL 或凭据；取消/崩溃留下 running 同样可重入。
                    if (!cancellationToken.IsCancellationRequested)
                        await Finish("failed", error is QianErrorException qian ? qian.Error.Code.Code : "step_failed", cancellationToken).ConfigureAwait(false);
                    throw;
                }
                async Task Finish(string status, string? reason, CancellationToken ct)
                {
                    await using var session = await context.Data().OpenAsync<TenancyDbContext>(ct).ConfigureAwait(false);
                    var step = await session.Context.ProvisionSteps.SingleAsync(x => x.Step == name, ct).ConfigureAwait(false);
                    step.Status = status; step.Reason = reason;
                    await session.SaveChangesAsync(ct).ConfigureAwait(false);
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<TenantProvisionStep>> GetStepsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Check();
        if (await directory.GetByIdAsync(id, cancellationToken).ConfigureAwait(false) is null) throw Error(PlatformErrors.NotFound);
        using var scope = _tenant.BeginScope(id.ToString());
        await using var session = await context.Data().OpenAsync<TenancyDbContext>(cancellationToken).ConfigureAwait(false);
        return await session.Context.ProvisionSteps.AsNoTracking().OrderBy(x => x.Step)
            .Select(x => new TenantProvisionStep(x.Step, x.Status, x.Attempts, x.Reason)).ToArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> Locked<T>(string key, Func<Task<T>> work, CancellationToken ct)
    {
        await using var session = await context.Data().OpenAsync<TenancyDbContext>(ct).ConfigureAwait(false);
        await session.Context.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        var connection = session.Context.Database.GetDbConnection();
        var lockKey = "qian.tenancy:" + key;
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_advisory_lock(hashtextextended(@key, 0))";
        var parameter = command.CreateParameter(); parameter.ParameterName = "key"; parameter.Value = lockKey; command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        try { return await work().ConfigureAwait(false); }
        finally
        {
            command.CommandText = "SELECT pg_advisory_unlock(hashtextextended(@key, 0))";
            await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
    private static void ValidateActor(string actor)
    { if (string.IsNullOrWhiteSpace(actor) || actor.Length > 200 || actor.Any(char.IsControl)) throw Error(PlatformErrors.InputInvalid); }
    private static QianErrorException Error(ErrorCode code) => new(new QianError(code));
    private sealed record ProvisionInput(CreateTenant Request, TenantAdministrator Administrator);
}
