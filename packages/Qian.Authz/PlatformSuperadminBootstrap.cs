using Kun.Core.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;

namespace Qian.Authz;

/// <summary>
/// 首个平台超管的一次性引导（S1-DEP-011，R150 ③）。平台超管只经内部平台角色存储授予（授予属 humanOnly 管理操作，不对 HTTP 暴露）；
/// 部署后还没有任何平台运营账号时，平台宿主的引导模式（<c>--bootstrap-operator</c>）经这里授出第一个。
/// 库里已有任何平台超管授予时不改动并返回 false：引导入口不能拿来给第二个人提权，之后的授予与撤销仍走平台运营流程。
/// </summary>
public static class PlatformSuperadminBootstrap
{
    /// <summary>库里是否已有平台超管授予（不论有效期）；引导前的只读预检，经同一个专用平台授权登录读取。</summary>
    public static Task<bool> ExistsAsync(IServiceProvider services, string platformRole, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        AuthzPlatformAccess.ValidateRoleName(platformRole);
        return new PostgresPlatformRoleStore(services.GetRequiredService<IDataService>(), services.GetRequiredService<ITenantContext>(), platformRole)
            .AnySuperadminAsync(cancellationToken);
    }

    /// <summary>
    /// 在无租户上下文中，经专用平台授权登录（<paramref name="platformRole"/>，宿主须已登记 <see cref="PlatformAuthzDbContext"/>）把平台超管授给
    /// <paramref name="userId"/>（平台身份的自然人 ID）。返回 true 表示已授予；已有平台超管时返回 false 且不改动。
    /// </summary>
    public static Task<bool> AssignFirstAsync(IServiceProvider services, string platformRole, string userId, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        AuthzPlatformAccess.ValidateRoleName(platformRole);
        return new PostgresPlatformRoleStore(services.GetRequiredService<IDataService>(), services.GetRequiredService<ITenantContext>(), platformRole)
            .AssignFirstSuperadminAsync(userId, actor, cancellationToken);
    }
}
