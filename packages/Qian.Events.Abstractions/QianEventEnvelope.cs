using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Qian.Events;

/// <summary>
/// 跨进程集成事件信封 v1，与 contracts/v0/event-envelope.schema.json 同形：Outbox 行与 RabbitMQ 消息体都是这份 JSON。
/// payload 由事件目录里该 <see cref="Type"/> 的 Schema 约束，信封不约束其内容；进程内运行时事件不用本信封。
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed partial class QianEventEnvelope
{
    /// <summary>信封版本常量；破坏性变更升 v2。</summary>
    public const string CurrentVersion = "v1";

    /// <summary>信封版本。</summary>
    [JsonPropertyName("version")] public required string Version { get; init; }
    /// <summary>事件实例 id（UUID v7），消费端幂等键。</summary>
    [JsonPropertyName("event_id"), JsonConverter(typeof(CanonicalUuidConverter))] public required Guid EventId { get; init; }
    /// <summary><c>&lt;domain&gt;.&lt;entity&gt;.&lt;verb&gt;</c>，等于 <see cref="Type"/> 去掉版本后缀，也是路由键。</summary>
    [JsonPropertyName("subject")] public required string Subject { get; init; }
    /// <summary>稳定事件 ID，即 <c>[IntegrationEventId]</c> 的值。</summary>
    [JsonPropertyName("type")] public required string Type { get; init; }
    /// <summary>业务发生时间，带时区。</summary>
    [JsonPropertyName("occurred_at"), JsonConverter(typeof(OffsetTimestampConverter))] public required DateTimeOffset OccurredAt { get; init; }
    /// <summary>租户；null 表示平台级事件，必须显式写出。</summary>
    [JsonPropertyName("tenant_id"), JsonIgnore(Condition = JsonIgnoreCondition.Never), JsonConverter(typeof(CanonicalUuidConverter))] public required Guid? TenantId { get; init; }
    /// <summary>发布方组件 id。</summary>
    [JsonPropertyName("producer")] public required string Producer { get; init; }
    /// <summary>业务关联 id。</summary>
    [JsonPropertyName("correlation_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonConverter(typeof(CanonicalUuidConverter))] public Guid? CorrelationId { get; init; }
    /// <summary>直接引起本事件的命令或事件 id。</summary>
    [JsonPropertyName("causation_id"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonConverter(typeof(CanonicalUuidConverter))] public Guid? CausationId { get; init; }
    /// <summary>触发事件的主体。</summary>
    [JsonPropertyName("actor")] public required EventActor Actor { get; init; }
    /// <summary>W3C traceparent。</summary>
    [JsonPropertyName("trace_parent")] public required string TraceParent { get; init; }
    /// <summary>事件数据，必须是 JSON 对象。</summary>
    [JsonPropertyName("payload")] public required JsonElement Payload { get; init; }

    /// <summary>序列化为信封 JSON；先做与 Schema 相同的语义校验。</summary>
    public string ToJson()
    {
        Validate();
        return JsonSerializer.Serialize(this, EventEnvelopeJson.Default.QianEventEnvelope);
    }

    /// <summary>解析并校验信封 JSON；不合法时以 <see cref="EventsErrors.PublishRejected"/> 拒绝。</summary>
    public static QianEventEnvelope Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        QianEventEnvelope? envelope;
        try { envelope = JsonSerializer.Deserialize(json, EventEnvelopeJson.Default.QianEventEnvelope); }
        catch (JsonException error) { throw QianEventsException.Rejected("信封不是合法的 v1 JSON：" + error.Message); }
        if (envelope is null) throw QianEventsException.Rejected("信封不能为 null。");
        envelope.Validate();
        return envelope;
    }

    /// <summary>按 contracts/v0 的规则校验字段，包括 Schema 表达不了的 subject 与 type 对应关系。</summary>
    public void Validate()
    {
        if (Version != CurrentVersion) Reject("version 必须为 v1。");
        if (EventId == Guid.Empty) Reject("event_id 不能为空 UUID。");
        if (!IntegrationEventTypes.TryParse(Type, out var subject, out _)) Reject("type 必须为 <subject>.v<N>。");
        if (Subject != subject) Reject("subject 必须等于 type 去掉版本后缀。");
        if (OccurredAt == default) Reject("occurred_at 不能为空。");
        if (TenantId == Guid.Empty) Reject("tenant_id 不能为空 UUID；平台级事件写 null。");
        if (!IsComponentId(Producer)) Reject("producer 必须为小写点分的组件 id。");
        if (CorrelationId == Guid.Empty || CausationId == Guid.Empty) Reject("correlation_id 与 causation_id 不能为空 UUID。");
        if (Actor is null) Reject("actor 必填。");
        Actor.Validate();
        if (!IsTraceParent(TraceParent)) Reject("trace_parent 必须为 W3C traceparent。");
        if (Payload.ValueKind != JsonValueKind.Object) Reject("payload 必须是 JSON 对象。");
    }

    /// <summary>组件 id 规则：小写点分、至少两段，允许连字符。</summary>
    public static bool IsComponentId([NotNullWhen(true)] string? value) => value is { Length: > 0 and <= 200 } && ComponentIdPattern().IsMatch(value);

    /// <summary>W3C traceparent 规则（版本 00）。</summary>
    public static bool IsTraceParent([NotNullWhen(true)] string? value) => value is { Length: 55 } && TraceParentPattern().IsMatch(value) &&
        !value.AsSpan(3, 32).SequenceEqual("00000000000000000000000000000000") && !value.AsSpan(36, 16).SequenceEqual("0000000000000000");

    [DoesNotReturn]
    private static void Reject(string reason) => throw QianEventsException.Rejected(reason);

    [GeneratedRegex("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex ComponentIdPattern();

    [GeneratedRegex("^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex TraceParentPattern();
}

/// <summary>触发事件的主体；取值与 S2S principal_kind、审计 actor_type 一致。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EventActor
{
    /// <summary>主体类型。</summary>
    [JsonPropertyName("kind")] public required EventActorKind Kind { get; init; }
    /// <summary>user 为 person_id，service / agent 为 client_id。</summary>
    [JsonPropertyName("id")] public required string Id { get; init; }
    /// <summary>agent 或 service 代表用户行事时的委托人 person_id。</summary>
    [JsonPropertyName("on_behalf_of"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? OnBehalfOf { get; init; }

    /// <summary>以用户身份触发。</summary>
    public static EventActor User(string personId) => new() { Kind = EventActorKind.User, Id = personId };
    /// <summary>以服务身份触发；定时作业等无人触发的事件也用执行它的服务主体。</summary>
    public static EventActor Service(string clientId, string? onBehalfOf = null) => new() { Kind = EventActorKind.Service, Id = clientId, OnBehalfOf = onBehalfOf };
    /// <summary>以 Agent 身份触发。</summary>
    public static EventActor Agent(string clientId, string? onBehalfOf = null) => new() { Kind = EventActorKind.Agent, Id = clientId, OnBehalfOf = onBehalfOf };

    internal void Validate()
    {
        if (!Enum.IsDefined(Kind)) throw QianEventsException.Rejected("actor.kind 只能是 user、service 或 agent。");
        if (Id is not { Length: > 0 and <= 255 } || string.IsNullOrWhiteSpace(Id)) throw QianEventsException.Rejected("actor.id 必须为 1 到 255 个字符。");
        if (OnBehalfOf is not null && (OnBehalfOf.Length is 0 or > 255 || string.IsNullOrWhiteSpace(OnBehalfOf)))
            throw QianEventsException.Rejected("actor.on_behalf_of 必须为 1 到 255 个字符。");
    }

    /// <inheritdoc />
    public override string ToString() => "EventActor(redacted)";
}

/// <summary>事件主体类型；agent 为 S4 Agent 触发器预留，S1 已可写入信封。</summary>
[JsonConverter(typeof(EventActorKindConverter))]
public enum EventActorKind
{
    /// <summary>自然人。</summary>
    User,
    /// <summary>机器服务。</summary>
    Service,
    /// <summary>Agent 客户端。</summary>
    Agent
}

internal sealed class EventActorKindConverter : JsonConverter<EventActorKind>
{
    public override EventActorKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType == JsonTokenType.String
        ? reader.GetString() switch { "user" => EventActorKind.User, "service" => EventActorKind.Service, "agent" => EventActorKind.Agent, _ => throw new JsonException("未知主体类型。") }
        : throw new JsonException("主体类型必须为字符串。");

    public override void Write(Utf8JsonWriter writer, EventActorKind value, JsonSerializerOptions options) => writer.WriteStringValue(value switch
    {
        EventActorKind.User => "user", EventActorKind.Service => "service", EventActorKind.Agent => "agent", _ => throw new JsonException("未知主体类型。")
    });
}

// 信封里的 UUID 只接受小写规范形式（与 s2s-claims.schema.json#/$defs/Uuid 一致），写出同样是小写。
internal sealed partial class CanonicalUuidConverter : JsonConverter<Guid>
{
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : throw new JsonException("UUID 必须为字符串。");
        return text is not null && Pattern().IsMatch(text) && Guid.TryParseExact(text, "D", out var value) ? value : throw new JsonException("UUID 必须为小写规范形式。");
    }

    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString("D"));

    [GeneratedRegex("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}

// RFC 3339 时间必须带时区；System.Text.Json 默认会把无时区的值当作本地时间接受。
internal sealed partial class OffsetTimestampConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : throw new JsonException("时间必须为字符串。");
        return text is not null && Pattern().IsMatch(text) && DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var value) ? value : throw new JsonException("时间必须为带时区的 RFC 3339 格式。");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToUniversalTime());

    [GeneratedRegex("^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\\.[0-9]{1,9})?(Z|[+-][0-9]{2}:[0-9]{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}

[JsonSourceGenerationOptions(RespectNullableAnnotations = true)]
[JsonSerializable(typeof(QianEventEnvelope))]
internal sealed partial class EventEnvelopeJson : JsonSerializerContext;
