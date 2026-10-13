using Qian.Runtime;

namespace Qian.Plugin.Sdk;

/// <summary>编译程序集暴露的生成元数据入口；只用于可信项目的构建期工具。</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class QianCodeMetadataAttribute(Type factoryType) : Attribute
{
    /// <summary>由源生成器产生、实现 IComponentCodeMetadataFactory 的工厂。</summary>
    public Type FactoryType { get; } = factoryType;
}

/// <summary>生成代码与清单工具之间的构建期协议，不实例化业务插件。</summary>
public interface IComponentCodeMetadataFactory
{
    /// <summary>读取生成的契约、服务键与入口。</summary>
    ComponentCodeMetadata Create();
}

/// <summary>代码派生的组件字段；手写版本、展示、策略和路径仍由唯一 plugin.json 保存。</summary>
public sealed class ComponentCodeMetadata
{
    /// <summary>复制生成的服务键与能力集合。</summary>
    public ComponentCodeMetadata(string componentId, string runtimeEntry, IReadOnlyList<ServiceKey> inject, IReadOnlyList<ServiceKey> provides, IReadOnlyList<CapabilityContract> capabilities)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId); ArgumentException.ThrowIfNullOrWhiteSpace(runtimeEntry);
        ArgumentNullException.ThrowIfNull(inject); ArgumentNullException.ThrowIfNull(provides); ArgumentNullException.ThrowIfNull(capabilities);
        ComponentId = componentId; RuntimeEntry = runtimeEntry;
        Inject = Array.AsReadOnly(inject.Select(key => key.Name).Order(StringComparer.Ordinal).ToArray());
        Provides = Array.AsReadOnly(provides.Select(key => key.Name).Order(StringComparer.Ordinal).ToArray());
        Capabilities = Array.AsReadOnly(capabilities.OrderBy(contract => contract.Name, StringComparer.Ordinal).ToArray());
        if (Inject.Distinct(StringComparer.Ordinal).Count() != Inject.Count || Provides.Distinct(StringComparer.Ordinal).Count() != Provides.Count || Inject.Intersect(Provides, StringComparer.Ordinal).Any() ||
            Capabilities.Any(c => c.ComponentId != componentId) || Capabilities.Select(c => c.Name).Distinct(StringComparer.Ordinal).Count() != Capabilities.Count)
            throw new ArgumentException("派生元数据包含重复能力、跨组件契约或冲突服务键。");
    }
    /// <summary>组件稳定 ID。</summary>
    public string ComponentId { get; }
    /// <summary>实现类型及程序集。</summary>
    public string RuntimeEntry { get; }
    /// <summary>从 Inject 属性派生的必需服务。</summary>
    public IReadOnlyList<string> Inject { get; }
    /// <summary>从 Apply 的 Provide 调用派生的服务。</summary>
    public IReadOnlyList<string> Provides { get; }
    /// <summary>代码中仍存在的能力。</summary>
    public IReadOnlyList<CapabilityContract> Capabilities { get; }
}
