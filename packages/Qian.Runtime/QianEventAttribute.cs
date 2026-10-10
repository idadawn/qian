namespace Qian.Runtime;

/// <summary>标注事件字段或属性的分发模式；文档生成器校验它与事件值及注册调用一致，不影响运行时求值。</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class QianEventAttribute : Attribute
{
    /// <summary>声明的分发模式；缺省为观察型 emit。</summary>
    public DispatchMode Mode { get; set; } = DispatchMode.Emit;
}
