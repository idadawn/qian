using System.Text.Json.Serialization;
using Qian.Identity.Abstractions;
using Qian.Runtime;

namespace Qian.Capabilities;

/// <summary>当前租户内可访问的菜单项；资源引用使用 qian:{tenant}:{app}:page:{id}。</summary>
public sealed record NavigationItem(string ResourceRef, string PluginId, string Route, string Title, string? Icon, int Order);

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
}
