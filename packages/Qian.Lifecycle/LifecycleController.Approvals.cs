using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Qian.Audit;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Lifecycle;

// 反向权限批准（PRD 7.18 第 6 项、plugin-manifest D7 / T5）：S1 只有 API，S2 才有 UI。批准记录在本组件的租户表 permission_approvals，
// 与权益（Entitled，「买没买」）是两道门、不合并；开通与升级在权益之后核对批准（「同不同意」）。
internal sealed partial class LifecycleController
{
    public async ValueTask<PermissionApprovalView> ApprovePermissionsAsync(string tenantId, string pluginId, string version, PermissionGrants? grants, ICurrentUser user,
        CancellationToken cancellationToken = default)
    {
        CheckManagement(tenantId, pluginId, version, user);
        // 批准必须入该租户审计链（ADR-009 D9.8）：宿主没有装配审计时与缺判定端口一样 fail-closed，不写记录；对外仍是 404（R32），
        // 但宿主装配错误须可诊断：先记一条不含载荷的宿主级告警（S1-RT-037）。
        if (context.Scoped(new ScopeKey("tenant", tenantId)).TryInject<IAuditService>(AuditDefinition.Key) is not { } audit)
        {
            LifecycleLog.AuditUnavailable(context.Logger, "permissions.approve", tenantId);
            throw NotFound();
        }
        var via = await AuthorizeAsync(tenantId, user, LifecycleDefinition.InstallApprovePermission, ApprovalResource(tenantId, pluginId, version), cancellationToken).ConfigureAwait(false);
        // 判定之后才校验内容与目标版本：无权限者得到的永远是同一个 404。
        var approved = PermissionSet.Read(grants);
        var revision = await Revision(row => row.PluginId == pluginId && row.Version == version, cancellationToken).ConfigureAwait(false) ?? throw NotFound();
        PermissionSet.RequireDeclared(grants, await Declared(revision, cancellationToken).ConfigureAwait(false));
        var approver = user.Principal!;
        for (var attempt = 0; ; attempt++)
        {
            try { return await Approve(audit, tenantId, pluginId, version, approved, approver, via, cancellationToken).ConfigureAwait(false); }
            catch (Exception error) when (attempt < 5 && Retryable(error))
            { await Task.Delay(10 * (attempt + 1), cancellationToken).ConfigureAwait(false); }
            catch (Exception error) when (Retryable(error)) { throw new QianErrorException(new QianError(PlatformErrors.Conflict)); }
        }
    }

    // 批准记录与审计条目在同一可串行化事务里提交：审计写不进去，批准也不生效。
    private Task<PermissionApprovalView> Approve(IAuditWriter audit, string tenantId, string pluginId, string version, PermissionSet approved, Principal approver, string via,
        CancellationToken ct) => Locked(["install", tenantId, pluginId], IsolationLevel.Serializable, async session =>
    {
        var row = await session.Context.PermissionApprovals.SingleOrDefaultAsync(item => item.PluginId == pluginId && item.Version == version, ct).ConfigureAwait(false);
        // 同一内容重复批准不改记录、不记审计（幂等）；内容不同则整份替换并记下这一次的批准人。
        if (row is not null && PermissionSet.Parse(row.Approved).SetEquals(approved)) return row.View();
        // 读回的 jsonb 文本由 PostgreSQL 重排键序、加空格；上一份内容按规范形重新序列化，哈希才与当时写入的一致。
        var previous = row is null ? null : PermissionSet.Parse(row.Approved).Serialize();
        if (row is null) session.Context.PermissionApprovals.Add(row = new PermissionApprovalRow { TenantId = tenantId, PluginId = pluginId, Version = version });
        row.Approved = approved.Serialize(); row.ApprovedBy = approver.Subject; row.ApprovedVia = via; row.ApprovedAt = Now(); row.RowVersion++;
        await Save(session, null, ct).ConfigureAwait(false);
        await audit.AppendEnlistedAsync(ApprovalRecord(row, previous, approver), session.Context.Database.CurrentTransaction!.GetDbTransaction(), ct).ConfigureAwait(false);
        return row.View();
    }, ct);

    // 租户链上的批准条目：主体即批准人，资源同判定资源，输入哈希取规范化的本次内容；载荷带渠道与本次、上一次的批准内容（首次批准时上一次为 null）。
    // 两份内容合起来超出审计载荷上限时只记各自的 SHA-256，完整内容在批准记录与上一条批准审计里。
    private static AuditRecord ApprovalRecord(PermissionApprovalRow row, string? previous, Principal approver)
    {
        var payload = ApprovalPayload(row, JsonNode.Parse(row.Approved), previous is null ? null : JsonNode.Parse(previous));
        if (payload.GetRawText().Length > AuditPayloadLimit) payload = ApprovalPayload(row, Digest(row.Approved), previous is null ? null : Digest(previous));
        var trace = Activity.Current?.TraceId is { } id && id != default ? id.ToHexString() : ActivityTraceId.CreateRandom().ToHexString();
        return new AuditRecord(Guid.CreateVersion7(), row.TenantId, approver.Kind == PrincipalKind.User ? "user" : "service", approver.Subject, null, ApprovalAuditCapability, "v1",
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(row.Approved))), "api", ApprovalResource(row.TenantId, row.PluginId, row.Version), "success", trace,
            row.ApprovedAt, payload);
        static JsonObject Digest(string content) => new() { ["sha256"] = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content))) };
    }

    private static JsonElement ApprovalPayload(PermissionApprovalRow row, JsonNode? approved, JsonNode? previous) => JsonSerializer.SerializeToElement(new JsonObject
    {
        ["pluginId"] = row.PluginId, ["version"] = row.Version, ["via"] = row.ApprovedVia, ["approved"] = approved, ["previous"] = previous
    });

    // 可串行化冲突或死锁：放弃整个会话，从新会话重做读取、写入与审计（审计端口的约定）。
    private static bool Retryable(Exception error)
    {
        var current = error;
        while (current is DbUpdateException or InvalidOperationException && current.InnerException is { } inner) current = inner;
        return current is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected };
    }

    public async ValueTask<PermissionApprovalView?> GetPermissionApprovalAsync(string tenantId, string pluginId, string version, ICurrentUser user, CancellationToken cancellationToken = default)
    {
        CheckManagement(tenantId, pluginId, version, user);
        await AuthorizeAsync(tenantId, user, LifecycleDefinition.InstallApprovePermission, ApprovalResource(tenantId, pluginId, version), cancellationToken).ConfigureAwait(false);
        return await Approval(pluginId, version, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<PermissionApprovalView?> GetApprovedPermissionsAsync(string tenantId, string pluginId, string version, CancellationToken cancellationToken = default)
    {
        CheckTenant(tenantId); CheckPlugin(pluginId); CheckVersion(version, "#/version");
        using var scope = tenant.BeginScope(tenantId);
        return await Approval(pluginId, version, cancellationToken).ConfigureAwait(false);
    }

    // 开通与升级的第二道门（在权益之后、置 Provisioning / Upgrading 之前，与安装行同一把锁）：policy.required 的平台组件、没有声明反向权限的组件不经批准；
    // 否则目标版本声明的每一项都须已批准。目标版本没有自己的批准记录时沿用该租户对该组件最近一次的批准，所以升级只有新增项才须重批（D7），
    // 连续升级不因中间版本没有记录而受阻。S1 只在错误里列出缺失项，不合并批准记录。
    private async Task RequireApproval(LifecycleDbContext db, PluginRevisionRow target, CancellationToken ct)
    {
        if (target.Required) return;
        var declared = await Declared(target, ct).ConfigureAwait(false);
        if (declared.IsEmpty) return;
        var rows = await db.PermissionApprovals.AsNoTracking().Where(row => row.PluginId == target.PluginId).ToListAsync(ct).ConfigureAwait(false);
        var approval = rows.SingleOrDefault(row => row.Version == target.Version) ??
            rows.OrderByDescending(row => row.ApprovedAt).ThenBy(row => row.Version, StringComparer.Ordinal).FirstOrDefault();
        var missing = declared.Except(approval is null ? PermissionSet.Empty : PermissionSet.Parse(approval.Approved));
        if (!missing.IsEmpty)
            throw new QianErrorException(new QianError(LifecycleErrors.PermissionsUnapproved, new Dictionary<string, string> { ["missing"] = missing.Describe() }));
    }

    // 修订声明的反向权限取自 Registry 保存的已校验清单快照（plugin_revisions 不存清单）；安装时已登记，缺失说明目录不一致，不能放行。
    private async Task<PermissionSet> Declared(PluginRevisionRow revision, CancellationToken ct) =>
        await context.Registry().GetArtifactPermissionsAsync(revision.ArtifactDigest, ct).ConfigureAwait(false) is { } declared
            ? PermissionSet.From(declared) : throw Conflict("注册表没有该修订的制品清单，不能判定反向权限。");

    private async Task<PermissionApprovalView?> Approval(string pluginId, string version, CancellationToken ct)
    {
        await using var session = await context.Data().OpenAsync<LifecycleDbContext>(ct).ConfigureAwait(false);
        return (await session.Context.PermissionApprovals.AsNoTracking().SingleOrDefaultAsync(row => row.PluginId == pluginId && row.Version == version, ct).ConfigureAwait(false))?.View();
    }

    // 管理面入口：参数里的目标租户只用来核对，不用来选择租户。它须等于宿主已建立的租户上下文：租户主体由租户边界按令牌建立，平台主体由运营宿主
    // 先选定已验证的目标租户（与 Registry 的平台主体入口同一口径，S1-AZ-008），这一核对在判定入口完成。标识格式不合格与无权限同为 404。
    private static void CheckManagement(string tenantId, string pluginId, string version, ICurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!user.IsAuthenticated || user.Principal is not { } principal || user.UserId != principal.Subject) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        try { CheckTenant(tenantId); CheckPlugin(pluginId); CheckVersion(version, "#/version"); }
        catch (QianErrorException) { throw NotFound(); }
        // 批准人取主体：与 Authz 对主体的约束一致（不超过 200 字符、不含控制字符），宿主换用别的判定端口时也不会写出超长的批准人。
        if (principal.Subject.Length > 200 || principal.Subject.Any(char.IsControl)) throw NotFound();
    }

    private static string ApprovalResource(string tenantId, string pluginId, string version) => "lifecycle/install:" + tenantId + "/" + pluginId + "@" + version;

    // 批准条目的稳定能力 ID（R23 component/name）与审计载荷上限（AuditRecord 拒绝超过 64 KiB 的载荷）。
    internal const string ApprovalAuditCapability = "qian.lifecycle/permissions.approve";
    private const int AuditPayloadLimit = 65536;
}
