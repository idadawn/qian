using Microsoft.EntityFrameworkCore;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Registry;
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
        var via = await AuthorizeAsync(tenantId, user, LifecycleDefinition.InstallApprovePermission, ApprovalResource(tenantId, pluginId, version), cancellationToken).ConfigureAwait(false);
        // 判定之后才校验内容与目标版本：无权限者得到的永远是同一个 404。
        var approved = PermissionSet.Read(grants);
        var revision = await Revision(row => row.PluginId == pluginId && row.Version == version, cancellationToken).ConfigureAwait(false) ?? throw NotFound();
        PermissionSet.RequireDeclared(grants, await Declared(revision, cancellationToken).ConfigureAwait(false));
        var approver = user.Principal!.Subject;
        return await Locked(["install", tenantId, pluginId], async session =>
        {
            var row = await session.Context.PermissionApprovals.SingleOrDefaultAsync(item => item.PluginId == pluginId && item.Version == version, cancellationToken).ConfigureAwait(false);
            // 同一内容重复批准不改记录（幂等）；内容不同则整份替换并记下这一次的批准人。
            if (row is not null && PermissionSet.Parse(row.Approved).SetEquals(approved)) return row.View();
            if (row is null) session.Context.PermissionApprovals.Add(row = new PermissionApprovalRow { TenantId = tenantId, PluginId = pluginId, Version = version });
            row.Approved = approved.Serialize(); row.ApprovedBy = approver; row.ApprovedVia = via; row.ApprovedAt = Now(); row.RowVersion++;
            await Save(session, null, cancellationToken).ConfigureAwait(false);
            return row.View();
        }, cancellationToken).ConfigureAwait(false);
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
    }

    private static string ApprovalResource(string tenantId, string pluginId, string version) => "lifecycle/install:" + tenantId + "/" + pluginId + "@" + version;
}
