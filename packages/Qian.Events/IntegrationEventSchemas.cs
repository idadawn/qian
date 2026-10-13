using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using Kun.EventBus;

namespace Qian.Events;

/// <summary>
/// 由登记时的源生成 JsonTypeInfo 导出 payload 的 JSON Schema（2020-12），与 Outbox 实际写出的 JSON 同一份契约；
/// 不用反射序列化补位：没有 JsonTypeInfo 的事件不能发布，也不进目录。
/// </summary>
public static class IntegrationEventSchemas
{
    private const string Dialect = "https://json-schema.org/draft/2020-12/schema";

    /// <summary>导出 Kun 事件目录中每个事件的描述，按事件 ID 排序；任一事件不合规即拒绝。</summary>
    public static IReadOnlyList<IntegrationEventDescriptor> Describe(IEventTypeRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.GetAll().OrderBy(item => item.EventId, StringComparer.Ordinal).Select(item =>
        {
            var typeInfo = registry.GetJsonTypeInfo(item.ClrType)
                ?? throw new QianEventsException(EventsErrors.Misconfigured, "事件 " + item.EventId + " 必须以源生成 JsonTypeInfo 登记。");
            if (!IntegrationEventTypes.TryParse(item.EventId, out var subject, out var version))
                throw new QianEventsException(EventsErrors.Misconfigured, "事件 ID " + item.EventId + " 必须为小写点分的 <subject>.v<N>。");
            Check(item.EventId, item.ClrType);
            return new IntegrationEventDescriptor(item.EventId, subject, version, Export(typeInfo));
        }).ToArray();
    }

    /// <summary>从 JsonTypeInfo 导出 payload Schema；payload 必须序列化为 JSON 对象。</summary>
    public static JsonElement Export(JsonTypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
            throw new QianEventsException(EventsErrors.Misconfigured, "事件 payload 必须序列化为 JSON 对象：" + typeInfo.Type.Name);
        var schema = JsonSchemaExporter.GetJsonSchemaAsNode(typeInfo, new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true });
        if (schema is not JsonObject root)
            throw new QianEventsException(EventsErrors.Misconfigured, "无法导出事件 payload Schema：" + typeInfo.Type.Name);
        root.Insert(0, "$schema", Dialect);
        return JsonSerializer.SerializeToElement(root, JsonNodeContext.Default.JsonObject);
    }

    // 类型上的 [IntegrationEventId] 与登记值必须一致，防止登记处和契约各写一份后漂移。
    private static void Check(string eventId, Type clrType)
    {
        var declared = clrType.GetCustomAttribute<IntegrationEventIdAttribute>()?.EventType;
        if (declared is not null && declared != eventId)
            throw new QianEventsException(EventsErrors.Misconfigured, "事件 " + clrType.Name + " 的 [IntegrationEventId] 与登记值不一致。");
    }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(JsonObject))]
internal sealed partial class JsonNodeContext : System.Text.Json.Serialization.JsonSerializerContext;
