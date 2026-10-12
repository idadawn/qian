namespace Qian.Authz.Abstractions;

/// <summary>默认租户角色模板；Grants 只能是已展开的本组件权限点，不携带成员或租户。</summary>
public sealed class RoleTemplate
{
    /// <summary>复制模板字段；授予主体和事务由 Provider 负责。</summary>
    public RoleTemplate(string role, IReadOnlyList<string> patterns, IReadOnlyList<string> grants)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role); ArgumentNullException.ThrowIfNull(patterns); ArgumentNullException.ThrowIfNull(grants);
        Role = role; Patterns = Array.AsReadOnly(patterns.ToArray()); Grants = Array.AsReadOnly(grants.ToArray());
    }
    /// <summary>角色稳定编码，例如 tenant.owner、tenant.user。</summary>
    public string Role { get; }
    /// <summary>作者声明的 grant 模式，保留用于版本升级时重新求值。</summary>
    public IReadOnlyList<string> Patterns { get; }
    /// <summary>本次组件版本中已展开的确切权限点。</summary>
    public IReadOnlyList<string> Grants { get; }
}

/// <summary>一个组件版本的权限声明编译结果；尚未实例化租户角色。</summary>
public sealed class PermissionCatalog
{
    /// <summary>复制编译结果。</summary>
    public PermissionCatalog(string pluginId, string version, IReadOnlyList<PermissionDefinition> permissions, IReadOnlyList<RoleTemplate> roles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId); ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(permissions); ArgumentNullException.ThrowIfNull(roles);
        PluginId = pluginId; Version = version; Permissions = Array.AsReadOnly(permissions.ToArray()); Roles = Array.AsReadOnly(roles.ToArray());
    }
    /// <summary>拥有这些声明的组件。</summary>
    public string PluginId { get; }
    /// <summary>声明所属版本。</summary>
    public string Version { get; }
    /// <summary>规范化后的全名权限点。</summary>
    public IReadOnlyList<PermissionDefinition> Permissions { get; }
    /// <summary>展开后的默认角色模板。</summary>
    public IReadOnlyList<RoleTemplate> Roles { get; }
}
