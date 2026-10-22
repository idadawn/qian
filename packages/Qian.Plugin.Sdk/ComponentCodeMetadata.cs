using Qian.Runtime;

namespace Qian.Plugin.Sdk;

/// <summary>编译程序集暴露的生成元数据入口；只用于可信项目的构建期工具。</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
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
        : this(componentId, runtimeEntry, inject, provides?.Select(key => key.Name).ToArray() ?? throw new ArgumentNullException(nameof(provides)), capabilities, "[]", "[]", null) { }

    /// <summary>复制完整代码声明；权限与事件使用既有 metadata 表面，配置使用独立 Schema。</summary>
    public ComponentCodeMetadata(string componentId, string runtimeEntry, IReadOnlyList<ServiceKey> inject, IReadOnlyList<string> provides,
        IReadOnlyList<CapabilityContract> capabilities, string permissionsJson, string eventsJson, string? configSchemaJson)
        : this(componentId, runtimeEntry, inject, provides, capabilities, permissionsJson, eventsJson, configSchemaJson, []) { }

    /// <summary>复制完整声明与可调度能力；既有构造入口保持兼容。</summary>
    public ComponentCodeMetadata(string componentId, string runtimeEntry, IReadOnlyList<ServiceKey> inject, IReadOnlyList<string> provides,
        IReadOnlyList<CapabilityContract> capabilities, string permissionsJson, string eventsJson, string? configSchemaJson, IReadOnlyList<JobContract> jobs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentId); ArgumentException.ThrowIfNullOrWhiteSpace(runtimeEntry);
        ArgumentNullException.ThrowIfNull(inject); ArgumentNullException.ThrowIfNull(provides); ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(jobs);
        if (jobs.Any(job => job.Capability.ComponentId != componentId || !capabilities.Contains(job.Capability)) || jobs.Select(job => job.Code).Distinct(StringComparer.Ordinal).Count() != jobs.Count)
            throw new ArgumentException("作业声明存在重复代码或跨组件能力。");
        Jobs = Array.AsReadOnly(jobs.OrderBy(job => job.Code, StringComparer.Ordinal).ToArray());
        ComponentId = componentId; RuntimeEntry = runtimeEntry;
        Inject = Array.AsReadOnly(inject.Select(key => key.Name).Order(StringComparer.Ordinal).ToArray());
        Provides = Array.AsReadOnly(provides.Order(StringComparer.Ordinal).ToArray());
        Capabilities = Array.AsReadOnly(capabilities.OrderBy(contract => contract.Name, StringComparer.Ordinal).ToArray());
        Permissions = Parse(permissionsJson, System.Text.Json.JsonValueKind.Array);
        Events = Parse(eventsJson, System.Text.Json.JsonValueKind.Array);
        ConfigSchema = configSchemaJson is null ? null : Parse(configSchemaJson, System.Text.Json.JsonValueKind.Object);
        var eventNames = Events.EnumerateArray().Select(item => item.GetProperty("name").GetString()).ToArray();
        if (eventNames.Any(string.IsNullOrWhiteSpace) || eventNames.Distinct(StringComparer.Ordinal).Count() != eventNames.Length)
            throw new ArgumentException("事件声明包含空名称或重复事件。");
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
    /// <summary>metadata/permissions.json 的权限资源数组。</summary>
    public System.Text.Json.JsonElement Permissions { get; }
    /// <summary>metadata/events.json 的事件声明数组。</summary>
    public System.Text.Json.JsonElement Events { get; }
    /// <summary>代码声明的配置 Schema；null 表示尚未声明。</summary>
    public System.Text.Json.JsonElement? ConfigSchema { get; }

    /// <summary>代码派生的 local 作业声明。</summary>
    public IReadOnlyList<JobContract> Jobs { get; }

    private static System.Text.Json.JsonElement Parse(string json, System.Text.Json.JsonValueKind kind)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != kind) throw new ArgumentException("派生元数据的 JSON 形状不正确。");
        return document.RootElement.Clone();
    }
}
