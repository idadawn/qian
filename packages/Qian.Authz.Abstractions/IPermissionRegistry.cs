namespace Qian.Authz.Abstractions;

/// <summary>权限声明的可逆注册端口；组件版本必须显式选择，登记本身不授予主体权限。</summary>
public interface IPermissionRegistry : IResourceTypeRegistry
{
    /// <summary>原子登记一批权限点；相同声明共享租约，冲突拒绝整批，释放只撤销当前租约。</summary>
    IDisposable Register(string version, IReadOnlyList<PermissionDefinition> permissions);
    /// <summary>按确切组件版本查找当前仍在注册生命周期内的权限点。</summary>
    PermissionDefinition? Find(string code, string version);
    /// <summary>列出组件某版本的活动权限点。</summary>
    IReadOnlyList<PermissionDefinition> List(string pluginId, string version);
}

/// <summary>qian.authz.resource-type/v1 资源类型扩展端口。</summary>
public interface IResourceTypeRegistry
{
    /// <summary>登记资源类型描述；与自动派生的资源身份一致，不创建权限点。</summary>
    IDisposable RegisterResources(string version, IReadOnlyList<ResourceTypeDefinition> resources);
    /// <summary>列出显式资源与权限点派生资源的合并视图。</summary>
    IReadOnlyList<ResourceTypeDefinition> ListResources(string pluginId, string version);
}

/// <summary>资源类型的稳定身份与展示说明；不包含资源实例或租户数据。</summary>
public sealed class ResourceTypeDefinition
{
    /// <summary>校验短名并生成组件前缀，不接受外部组件资源伪装成本组件。</summary>
    public ResourceTypeDefinition(string pluginId, string resource, string? description = null)
    {
        _ = PermissionCode.Format(pluginId, resource, "read");
        PluginId = pluginId; Resource = resource; Code = pluginId + ":" + resource; Description = description;
    }
    /// <summary>所属组件。</summary>
    public string PluginId { get; }
    /// <summary>组件内短名。</summary>
    public string Resource { get; }
    /// <summary>完整资源类型标识。</summary>
    public string Code { get; }
    /// <summary>作者提供的资源说明。</summary>
    public string? Description { get; }
}
