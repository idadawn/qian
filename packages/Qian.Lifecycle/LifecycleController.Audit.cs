using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore.Storage;
using Qian.Audit;
using Qian.Data;
using Qian.Events;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Lifecycle;

// 生命周期操作入审计链（ADR-009 D9.8，S1-RT-037）。
// 租户级操作（开通、升级、卸载、清除）：完成的状态迁移与该租户审计链的条目在同一个可串行化事务里提交，审计写不进去或提交失败时两者都不留；
// 只在最终迁移处记一条，所以钩子重试、续跑与幂等返回不重复记。
// 平台级操作（安装、激活、回切、弃用、退役）：没有租户，记平台链。审计端口的平台链入口是独立事务（专用平台登录，RLS 只放行该登录，不能挂进
// 本组件的事务），所以在状态变更提交前追加：审计写不进去则状态不提交；之后的提交失败可能留下一条孤儿条目（ADR-009 §3 的崩溃窗口取「宁多不漏」）。
internal sealed partial class LifecycleController
{
    internal const string ProvisionAuditCapability = "qian.lifecycle/tenant-plugin.provision";
    internal const string UpgradeAuditCapability = "qian.lifecycle/tenant-plugin.upgrade";
    internal const string UninstallAuditCapability = "qian.lifecycle/tenant-plugin.uninstall";
    internal const string PurgeAuditCapability = "qian.lifecycle/tenant-plugin.purge";
    internal const string InstallAuditCapability = "qian.lifecycle/plugin-revision.install";
    internal const string ActivateAuditCapability = "qian.lifecycle/plugin-revision.activate";
    internal const string RollbackAuditCapability = "qian.lifecycle/plugin-revision.rollback";
    internal const string DeprecateAuditCapability = "qian.lifecycle/plugin-revision.deprecate";
    internal const string RetireAuditCapability = "qian.lifecycle/plugin-revision.retire";
    private const string LifecycleAuditContract = "v1";

    // 该租户 scope 可见的审计服务。宿主没有装配时 fail-closed：记一条不含载荷的宿主级告警，再以稳定错误码拒绝，让装配错误与状态冲突、无权限可区分。
    private IAuditWriter RequireAudit(string tenantId, string operation)
    {
        if (context.Scoped(new ScopeKey("tenant", tenantId)).TryInject<IAuditService>(AuditDefinition.Key) is { } audit) return audit;
        LifecycleLog.AuditUnavailable(context.Logger, operation, tenantId);
        throw AuditUnavailable(operation);
    }

    // 平台链：本组件 global 上下文可见的审计服务；宿主没有装配时同样先记告警（事件 8）再拒绝。平台链有没有配置（chain.platformRole）只有追加时才知道，
    // 见 AppendPlatformAsync。
    private IAuditWriter RequirePlatformAudit(string operation)
    {
        if (context.TryInject<IAuditService>(AuditDefinition.Key) is { } audit) return audit;
        LifecycleLog.PlatformAuditUnavailable(context.Logger, operation, "宿主没有装配 qian.audit");
        throw AuditUnavailable(operation);
    }

    private static QianErrorException AuditUnavailable(string operation) =>
        new(new QianError(LifecycleErrors.AuditUnavailable, new Dictionary<string, string> { ["operation"] = operation }));

    // 状态迁移与审计条目同一可串行化事务（审计端口只接受这种事务）：work 先保存再返回记录，记录为 null 表示本次没有迁移（幂等返回），不追加。
    // 同租户审计链的可串行化冲突或死锁时丢弃整个会话重做，最多 5 次，仍冲突返回 409 CONFLICT（审计端口的重做约定，与批准相同）。
    private async Task<T> Audited<T>(string[] key, IAuditWriter audit, Func<IDataSession<LifecycleDbContext>, Task<(T Result, AuditRecord? Record)>> work, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await Locked(key, IsolationLevel.Serializable, async session =>
                {
                    var (result, record) = await work(session).ConfigureAwait(false);
                    if (record is not null) await audit.AppendEnlistedAsync(record, session.Context.Database.CurrentTransaction!.GetDbTransaction(), ct).ConfigureAwait(false);
                    return result;
                }, ct).ConfigureAwait(false);
            }
            catch (Exception error) when (attempt < 5 && Retryable(error))
            { await Task.Delay(10 * (attempt + 1), ct).ConfigureAwait(false); }
            catch (Exception error) when (Retryable(error)) { throw new QianErrorException(new QianError(PlatformErrors.Conflict)); }
        }
    }

    // 平台级状态变更与平台链条目：work 先保存再返回记录（null 表示幂等返回，不追加），记录在本事务提交之前追加到平台链（审计端口自己的事务）；
    // 追加失败则本事务不提交、状态未改。平台链自身的可串行化冲突或死锁由审计端口换新会话重做（5 次重做、共 6 次尝试），仍失败时原样抛出数据库异常
    // （PostgresException / DbUpdateException），数据库异常里只有唯一键冲突转 409 CONFLICT（见 PostgresAuditStore.AppendAsync）；两者都经这里原样传出，本事务同样不提交。
    private Task<T> PlatformAudited<T>(string[] key, string operation, IAuditWriter audit, Func<IDataSession<LifecycleDbContext>, Task<(T Result, AuditRecord? Record)>> work, CancellationToken ct) =>
        Locked(key, async session =>
        {
            var (result, record) = await work(session).ConfigureAwait(false);
            if (record is not null) await AppendPlatformAsync(audit, operation, record, ct).ConfigureAwait(false);
            return result;
        }, ct);

    // 平台链未配置（宿主没给 chain.platformRole）或本上下文不被接受为平台生产方时，审计端口返回 AUTHZ_DENIED：对调用方这是宿主装配错误而不是无权限，
    // 记告警后以 503 LIFECYCLE_AUDIT_UNAVAILABLE 拒绝（本事务随之回滚）。其它失败（链冲突、数据库错误）原样传出，同样不提交。
    private async Task AppendPlatformAsync(IAuditWriter audit, string operation, AuditRecord record, CancellationToken ct)
    {
        try { await audit.AppendPlatformAsync(context, record, ct).ConfigureAwait(false); }
        catch (QianErrorException error) when (error.Error.Code.Code == PlatformErrors.AuthzDenied.Code)
        {
            LifecycleLog.PlatformAuditUnavailable(context.Logger, operation, "审计端口拒绝本组件为平台生产方（端口不区分原因）：平台链未配置 chain.platformRole，或审计重挂后本组件持有的是陈旧的审计句柄");
            throw AuditUnavailable(operation);
        }
    }

    // 租户链上的生命周期条目：主体取调用方打开的事件作用域（与集成事件同一口径），没有时记本组件服务主体，操作者字符串进载荷；
    // 资源同批准的判定资源 lifecycle/install:{tenant}/{plugin}@{version}；输入哈希是 [租户, 组件, 能力, 版本…] 的 SHA-256；通道 api；发生时间即安装行的更新时间。
    private static AuditRecord TenantRecord(string capability, string tenantId, string pluginId, string version, DateTimeOffset occurredAt, JsonObject payload, params string[] versions)
    {
        var (actorType, actor) = Actor();
        return new AuditRecord(Guid.CreateVersion7(), tenantId, actorType, actor.Id, actor.OnBehalfOf, capability, LifecycleAuditContract,
            Hash([tenantId, pluginId, capability, .. versions]), "api", ApprovalResource(tenantId, pluginId, version), "success", Trace(), occurredAt, JsonSerializer.SerializeToElement(payload));
    }

    // 平台链上的生命周期条目：无租户、无委托约定同上；资源 lifecycle/revision:{plugin}/{revision}；输入哈希是 [组件, 能力, 修订号, 版本…] 的 SHA-256；
    // 发生时间即修订行上对应的时间戳（安装 InstalledAt、激活 / 回切 ActivatedAt），弃用与退役取操作时刻。
    private static AuditRecord PlatformRecord(string capability, string pluginId, int revision, DateTimeOffset occurredAt, JsonObject payload, params string[] versions)
    {
        var (actorType, actor) = Actor();
        return new AuditRecord(Guid.CreateVersion7(), null, actorType, actor.Id, actor.OnBehalfOf, capability, LifecycleAuditContract,
            Hash([pluginId, capability, revision.ToString(CultureInfo.InvariantCulture), .. versions]), "api", RevisionResource(pluginId, revision), "success", Trace(), occurredAt,
            JsonSerializer.SerializeToElement(payload));
    }

    internal static string RevisionResource(string pluginId, int revision) => "lifecycle/revision:" + pluginId + "/" + revision.ToString(CultureInfo.InvariantCulture);

    private static (string Type, EventActor Actor) Actor()
    {
        var actor = EventEnvelopeScope.Current?.Actor ?? EventActor.Service("qian.lifecycle");
        return (actor.Kind switch { EventActorKind.User => "user", EventActorKind.Agent => "agent", _ => "service" }, actor);
    }

    private static string Trace() => Activity.Current?.TraceId is { } id && id != default ? id.ToHexString() : ActivityTraceId.CreateRandom().ToHexString();

    private static string Hash(string[] parts) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(parts)));

    // 载荷只收组件、操作者与少量标量字段：修订号记为数字，版本、摘要与状态记为字符串，null 原样保留。
    private static JsonObject LifecyclePayload(string pluginId, string actor, params (string Name, object? Value)[] fields)
    {
        var payload = new JsonObject { ["pluginId"] = pluginId, ["operator"] = actor };
        foreach (var (name, value) in fields)
            payload[name] = value switch
            {
                null => null,
                string text => JsonValue.Create(text),
                int number => JsonValue.Create(number),
                _ => throw new ArgumentException("载荷字段只接受字符串与整数。", nameof(fields))
            };
        return payload;
    }
}
