namespace Qian.Plugin.Sdk;

/// <summary>引用与真实执行绑定共用的静态能力契约；不得通过实例化插件或处理器取得。</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class CapabilitySourceAttribute(Type owner, string member) : Attribute
{
    /// <summary>静态契约所属类型。</summary>
    public Type Owner { get; } = owner;
    /// <summary>只读静态 CapabilityContract 字段或属性名。</summary>
    public string Member { get; } = member;
}

/// <summary>稳定组件入口委托给已分类的实现构件；必须同时独立声明其完整服务输出。</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DelegatePluginAttribute(Type implementation) : Attribute
{
    /// <summary>本入口使用的具体实现类型；不允许接口或任意辅助方法。</summary>
    public Type Implementation { get; } = implementation;
}

/// <summary>委托入口的单项服务输出；直接 Apply 仍由语义分析派生，不使用此声明覆盖分析。</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ProvidesAttribute(Type keyOwner, string keyMember) : Attribute
{
    /// <summary>服务键所属类型。</summary>
    public Type KeyOwner { get; } = keyOwner;
    /// <summary>公开静态只读 ServiceKey 字段名。</summary>
    public string KeyMember { get; } = keyMember;
}

/// <summary>声明具名扩展点贡献；服务键由指定类型的公开静态只读 ServiceKey 字段取得。</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ContributesAttribute(Type keyOwner, string keyMember, string name) : Attribute
{
    /// <summary>服务键所属类型。</summary>
    public Type KeyOwner { get; } = keyOwner;
    /// <summary>服务键字段名。</summary>
    public string KeyMember { get; } = keyMember;
    /// <summary>固定贡献名。</summary>
    public string Name { get; } = name;
    /// <summary>Register 的真实注册表类型；普通同名方法不能冒充贡献。</summary>
    public Type? RegistryType { get; set; }
}

/// <summary>组件配置的代码声明；工具派生为清单所指的 configSchema 文件。</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class QianConfigSchemaAttribute(string json) : Attribute
{
    /// <summary>完整的 JSON Schema；应与实现共用同一常量。</summary>
    public string Json { get; } = json;
}

/// <summary>在多组件程序集内为权限或事件声明指定唯一归属。</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
public sealed class ComponentOwnerAttribute(string componentId) : Attribute
{
    /// <summary>所属组件 ID。</summary>
    public string ComponentId { get; } = componentId;
}
