using System.Text.Json;
using System.Text.Json.Serialization;
using Qian.Identity.Abstractions;
using Qian.Runtime;

namespace Qian.Capabilities;

/// <summary>当前租户内可访问的菜单项；资源引用使用 qian:{tenant}:{app}:page:{id}。</summary>
public sealed record NavigationItem(string ResourceRef, string PluginId, string Route, string Title, string? Icon, int Order);

/// <summary>仅限当前主体缓存的菜单结果。</summary>
public sealed record NavigationCatalog(IReadOnlyList<NavigationItem> Items)
{
    /// <summary>固定为 private。</summary>
    public string CacheScope { get; } = "private";
}

/// <summary>
/// 外壳目录（<c>GET /api/capabilities/shell</c>）：当前租户，以及本租户 scope 视图里实际挂载、Ready、带 UI 清单的组件。
/// 未挂载、被 restrict 隐藏或已卸载的组件不出现，它们的远程清单地址也就不会交给外壳；页面是否可见仍由外壳按有效权限过滤，每次调用由服务端重新判定。
/// </summary>
public sealed record ShellCatalog(ShellTenant Tenant, IReadOnlyList<ShellRemote> Remotes)
{
    /// <summary>固定为 private。</summary>
    public string CacheScope { get; } = "private";
}

/// <summary>当前租户的标识与显示名。</summary>
public sealed record ShellTenant(string Id, string Name);

/// <summary>一个已挂载组件的 UI 远程：组件 id、实际挂载的版本、所在 scope（<c>global</c> 或 <c>tenant:{id}</c>）与制品快照里 ui/manifest.json 的原文。</summary>
public sealed record ShellRemote(string Id, string Version, string Scope, JsonElement Manifest);

/// <summary>当前调用者的展示偏好，不包含主体或租户字段，也不授予权限。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NavigationPreference(bool Hidden, int? Order = null);

/// <summary>qian.navigation 在 S1 属于 Capabilities 的内部能力，不是另一个平台组件。</summary>
public interface INavigationService
{
    /// <summary>按实际挂载、权限和当前主体偏好返回菜单。</summary>
    ValueTask<IReadOnlyList<NavigationItem>> ListAsync(IPluginContext caller, ICurrentUser user, CancellationToken cancellationToken = default);
    /// <summary>解析资源引用；未知、无权限与已卸载均返回 NOT_FOUND。个人隐藏不等于撤权。</summary>
    ValueTask<NavigationItem> GetAsync(IPluginContext caller, ICurrentUser user, string resourceRef, CancellationToken cancellationToken = default);
    /// <summary>修改当前主体的展示偏好；只接受当前实际可访问的资源。</summary>
    ValueTask SetPreferenceAsync(IPluginContext caller, ICurrentUser user, string resourceRef, NavigationPreference preference, CancellationToken cancellationToken = default);
    /// <summary>外壳目录：当前租户与本租户 scope 实际挂载的 UI 远程；租户不在启用或试用状态时远程为空。主体与租户只来自已认证调用者。</summary>
    ValueTask<ShellCatalog> GetShellAsync(IPluginContext caller, ICurrentUser user, CancellationToken cancellationToken = default);
}
