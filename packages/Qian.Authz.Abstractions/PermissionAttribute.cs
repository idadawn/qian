namespace Qian.Authz.Abstractions;

/// <summary>独立宿主声明权限点；它是元数据，不代替入口权限检查。</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class PermissionAttribute(string resource, string action) : Attribute
{
    /// <summary>组件内的资源短名。</summary>
    public string Resource { get; } = resource;
    /// <summary>组件内的动作短名。</summary>
    public string Action { get; } = action;
    /// <summary>未单独标记的动作沿用低风险元数据，不因此获得任何权限。</summary>
    public PermissionRisk Risk { get; set; } = PermissionRisk.Low;
    /// <summary>是否要求提升认证强度。</summary>
    public bool RequiresStepUp { get; set; }
    /// <summary>声明适用终端；空集合表示未作终端限定，不是客户端可授予的值。</summary>
    public string[] Terminals { get; set; } = [];
}
