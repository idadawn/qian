using Qian.Runtime.Errors;

namespace Qian.Plugin.Sdk;

/// <summary>
/// 能力登记期的契约检查端口。平台宿主登记实现后，<see cref="CapabilityBindingRegistration.ProvideCapability{TRequest, TResponse}"/>
/// 在登记前调用它：生成目录条目并预编译输入校验器，不合格的契约在登记阶段拒绝，不留到首次调用。platform profile 宿主必须登记
/// （<c>AddQianCapabilities()</c>）；未登记时不做此检查，独立宿主没有通用执行入口，不需要它。
/// </summary>
public interface ICapabilityContractCheck
{
    /// <summary>检查并缓存一个能力契约；不合格时抛出 <see cref="CapabilityContractException"/>。</summary>
    void Check(CapabilityContract contract);
}

/// <summary>
/// 能力契约在登记阶段被拒绝：带组件 ID、能力短名、出错位置与规则。同样的值写入 <see cref="Exception.Data"/>（含原因码 <c>INPUT_INVALID</c>），
/// 供 scope 装载失败与管理面诊断读取；消息不含 Schema 或配置原文。
/// </summary>
public sealed class CapabilityContractException : InvalidOperationException
{
    /// <summary>创建一次登记拒绝。</summary>
    /// <param name="componentId">拥有该能力的组件。</param>
    /// <param name="name">组件内能力短名。</param>
    /// <param name="location">出错位置：以能力 ID 为文件的 JSON Pointer，如 <c>qian.sample.crm/customer.create#/inputSchema/properties/tenantId</c>。</param>
    /// <param name="rule">违反的规则：<c>tool-invalid</c>、<c>entry-invalid</c>、<c>tenant-field</c>、<c>unsupported-key</c>、<c>unsupported-number</c>、<c>unresolved-reference</c>、<c>schema-invalid</c>，
    /// 其余无法生成条目的情况为 <c>contract-invalid</c>。</param>
    /// <param name="innerException">底层失败；只用于诊断，不进入消息。</param>
    public CapabilityContractException(string componentId, string name, string location, string rule, Exception? innerException = null)
        : base($"组件 {componentId} 的能力 {name} 契约不合格（{rule}），位置 {location}，登记被拒绝。", innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId); ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(location); ArgumentException.ThrowIfNullOrWhiteSpace(rule);
        ComponentId = componentId; Name = name; Location = location; Rule = rule;
        Data["componentId"] = componentId; Data["capability"] = componentId + "/" + name; Data["location"] = location;
        Data["rule"] = rule; Data["reasonCode"] = ReasonCode;
    }

    /// <summary>拥有该能力的组件。</summary>
    public string ComponentId { get; }
    /// <summary>组件内能力短名。</summary>
    public string Name { get; }
    /// <summary>出错位置（JSON Pointer）。</summary>
    public string Location { get; }
    /// <summary>违反的规则。</summary>
    public string Rule { get; }
    /// <summary>对应的平台错误码，恒为 <c>INPUT_INVALID</c>。</summary>
    public string ReasonCode { get; } = PlatformErrors.InputInvalid.Code;
}
