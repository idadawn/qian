namespace Qian.Plugin.Sdk;

/// <summary>声明组件的稳定 ID；同程序集允许多个不同 ID 的入口。</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class QianPluginAttribute(string id) : Attribute
{
    /// <summary>组件 ID，不包含版本。</summary>
    public string Id { get; } = id;
    /// <summary>属于本组件的外部声明类型，例如 Definition 包中的事件与权限声明。</summary>
    public Type[] MetadataTypes { get; set; } = [];
}

/// <summary>声明命令或查询的契约；源生成器验证并生成强类型能力定义。</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class CapabilityAttribute(string name, Type responseType, Type serializerContext) : Attribute
{
    /// <summary>所属组件 ID；单组件程序集可省略，多组件程序集必须明确填写。</summary>
    public string? ComponentId { get; set; }
    /// <summary>组件内短名。</summary>
    public string Name { get; } = name;
    /// <summary>能力的响应类型。</summary>
    public Type ResponseType { get; } = responseType;
    /// <summary>包含请求与响应的 System.Text.Json 源生成上下文。</summary>
    public Type SerializerContext { get; } = serializerContext;
    /// <summary>面向调用者的意图式描述。</summary>
    public string Description { get; set; } = "";
    /// <summary>冻结契约中的副作用类型。</summary>
    public string SideEffect { get; set; } = "read";
    private string? _policyHint;
    /// <summary>冻结契约中的策略提示；destructive 默认 confirm。</summary>
    public string PolicyHint { get => _policyHint ?? (SideEffect == "destructive" ? "confirm" : "allow"); set => _policyHint = value; }
    /// <summary>写操作必须具有幂等键。</summary>
    public bool Idempotent { get; set; }
    /// <summary>none 或 task。</summary>
    public string Async { get; set; } = "none";
    /// <summary>执行所需 OAuth scope。</summary>
    public string[] RequiredScopes { get; set; } = [];
    /// <summary>执行所需权限点；不是令牌内授权。</summary>
    public string? RequiredPermission { get; set; }
    /// <summary>所需 Secret 名称，不包含值。</summary>
    public string[] RequiredSecrets { get; set; } = [];
    /// <summary>数据分级。</summary>
    public string DataClassification { get; set; } = "internal";
    /// <summary>成本级别。</summary>
    public string CostTier { get; set; } = "low";
    /// <summary>稳定契约族版本。</summary>
    public string ContractVersion { get; set; } = "v1";
    private bool? _audit;
    /// <summary>是否审计；未显式赋值时按命令或查询默认值派生。</summary>
    public bool Audit { get => _audit ?? SideEffect != "read"; set => _audit = value; }
    /// <summary>是否向 Agent 暴露。</summary>
    public bool AgentExpose { get; set; }
    /// <summary>Agent 调用是否要求确认。</summary>
    public bool RequiresConfirmation { get; set; }
}

/// <summary>将输入属性标为敏感；生成契约在实际 JSON 字段上添加 sensitive，而非把原值写入审计。</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class SensitiveAttribute : Attribute;
