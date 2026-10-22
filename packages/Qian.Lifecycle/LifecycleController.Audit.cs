using System.Data;
using System.Diagnostics;
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

// 生命周期操作入审计链（ADR-009 D9.8，S1-RT-037）：租户级操作（开通、升级、卸载、清除）完成的状态迁移与该租户审计链的条目在同一个可串行化事务里提交，
// 审计写不进去或提交失败时两者都不留；只在最终迁移处记一条，所以钩子重试、续跑与幂等返回不重复记。平台级操作（安装、激活、回切、弃用、退役）
// 没有租户，口径见 README「生命周期操作入审计链」。
internal sealed partial class LifecycleController
{
    internal const string ProvisionAuditCapability = "qian.lifecycle/tenant-plugin.provision";
    internal const string UpgradeAuditCapability = "qian.lifecycle/tenant-plugin.upgrade";
    internal const string UninstallAuditCapability = "qian.lifecycle/tenant-plugin.uninstall";
    internal const string PurgeAuditCapability = "qian.lifecycle/tenant-plugin.purge";
    private const string LifecycleAuditContract = "v1";

    // 该租户 scope 可见的审计服务。宿主没有装配时 fail-closed：记一条不含载荷的宿主级告警，再以稳定错误码拒绝，让装配错误与状态冲突、无权限可区分。
    private IAuditWriter RequireAudit(string tenantId, string operation)
    {
        if (context.Scoped(new ScopeKey("tenant", tenantId)).TryInject<IAuditService>(AuditDefinition.Key) is { } audit) return audit;
        LifecycleLog.AuditUnavailable(context.Logger, operation, tenantId);
        throw new QianErrorException(new QianError(LifecycleErrors.AuditUnavailable, new Dictionary<string, string> { ["operation"] = operation }));
    }

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

    // 租户链上的生命周期条目：主体取调用方打开的事件作用域（与集成事件同一口径），没有时记本组件服务主体，操作者字符串进载荷；
    // 资源同批准的判定资源 lifecycle/install:{tenant}/{plugin}@{version}；输入哈希是 [租户, 组件, 能力, 版本…] 的 SHA-256；通道 api；发生时间即安装行的更新时间。
    private static AuditRecord TenantRecord(string capability, string tenantId, string pluginId, string version, DateTimeOffset occurredAt, JsonObject payload, params string[] versions)
    {
        var actor = EventEnvelopeScope.Current?.Actor ?? EventActor.Service("qian.lifecycle");
        var actorType = actor.Kind switch { EventActorKind.User => "user", EventActorKind.Agent => "agent", _ => "service" };
        var trace = Activity.Current?.TraceId is { } id && id != default ? id.ToHexString() : ActivityTraceId.CreateRandom().ToHexString();
        var input = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new[] { tenantId, pluginId, capability }.Concat(versions).ToArray())));
        return new AuditRecord(Guid.CreateVersion7(), tenantId, actorType, actor.Id, actor.OnBehalfOf, capability, LifecycleAuditContract, input, "api",
            ApprovalResource(tenantId, pluginId, version), "success", trace, occurredAt, JsonSerializer.SerializeToElement(payload));
    }

    private static JsonObject LifecyclePayload(string pluginId, string actor, params (string Name, string? Value)[] fields)
    {
        var payload = new JsonObject { ["pluginId"] = pluginId, ["operator"] = actor };
        foreach (var (name, value) in fields) payload[name] = value;
        return payload;
    }
}
