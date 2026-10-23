using System.Collections.Concurrent;
using Qian.Identity.Abstractions;

namespace Qian.Dev.Shell;

// 开发期的身份修订下限，照 qian.authz 的 IdentityRevisionGuard 语义：令牌 identity_revision 低于见过的下限即旧令牌，401，客户端据此换令牌（S1-DEC-027）。
// 平台表面宿主的能力导航 / 有效权限端点没有权限点可声明、走不到 PEP，所以由宿主自己比对；下限只由开发路由抬高：/__dev/identity/bump 在经真实身份端口
// 递增修订之后抬到新修订，/__dev/identity/stale 只抬到当前令牌修订 + 1、不递增修订（刷新仍成功而平台表面持续 401）。
internal sealed class DevRevisionFloor
{
    private readonly ConcurrentDictionary<(string Tenant, string Subject), long> _floors = new();

    internal void Raise(string tenantId, string subject, long revision) =>
        _floors.AddOrUpdate((tenantId, subject), revision, (_, current) => Math.Max(current, revision));

    internal bool IsStale(Principal principal) =>
        principal is { Kind: PrincipalKind.User, TenantId: { } tenant, IdentityRevision: { } presented } && _floors.TryGetValue((tenant, principal.Subject), out var floor) && presented < floor;
}
