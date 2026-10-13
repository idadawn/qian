using Kun.Core.Domain;
using Kun.EventBus;
using Qian.Runtime.Errors;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qian.Data;

/// <summary>动态记录规则；主键策略首次保存后不可改，S2 引擎执行这些策略。</summary>
public sealed record DataRulePolicy(string PrimaryKeyStrategy, bool OptimisticConcurrency, bool SoftDelete,
    bool RecordHistory, IReadOnlyList<string> BusinessKey);
/// <summary>可信运行上下文提供的事件主体，与冻结信封 Actor 同形。</summary>
public sealed record RecordActor(string Kind, string Id, [property: JsonPropertyName("on_behalf_of")] string? OnBehalfOf = null);
/// <summary>记录变更载荷；快照必须按字段权限和敏感字段策略裁剪。</summary>
public sealed record RecordChangedPayload(string EntityCode, string RecordId, string Op, Guid TenantId, RecordActor Actor,
    IReadOnlyList<string> ChangedFields, JsonElement? Before = null, JsonElement? After = null)
{
    /// <inheritdoc />
    public override string ToString() => "RecordChangedPayload(redacted)";
}
/// <summary>qian.record.changed.v1 的完整 v0 信封；经同事务 Outbox 投递，after Hook 以 EventId 幂等。</summary>
[IntegrationEventId(EventType)]
public sealed record RecordChangedEvent(
    [property: JsonPropertyName("event_id")] Guid EventId,
    [property: JsonPropertyName("occurred_at")] DateTime OccurredAt,
    [property: JsonPropertyName("tenant_id")] Guid TenantId,
    string Producer, RecordActor Actor,
    [property: JsonPropertyName("trace_parent")] string TraceParent,
    RecordChangedPayload Payload,
    [property: JsonPropertyName("correlation_id")] Guid? CorrelationId = null,
    [property: JsonPropertyName("causation_id")] Guid? CausationId = null) : IIntegrationEvent
{
    /// <summary>稳定事件标识；载荷破坏性变更必须发布新的版本。</summary>
    public const string EventType = "qian.record.changed.v1";
    /// <summary>冻结信封版本。</summary>
    public string Version { get; } = "v1";
    /// <summary>事件主题。</summary>
    public string Subject { get; } = "qian.record.changed";
    /// <summary>事件目录查找键。</summary>
    public string Type { get; } = EventType;
    /// <summary>Outbox 入队前核对信封与载荷上下文；结构仍须通过发布的 JSON Schema。</summary>
    public void EnsureConsistentContext()
    {
        if (EventId == Guid.Empty || TenantId == Guid.Empty || Actor is null || Payload is null
            || OccurredAt.Kind != DateTimeKind.Utc || TenantId != Payload.TenantId || Actor != Payload.Actor)
            throw new InvalidOperationException("记录事件的租户、主体或 UTC 时间不一致。");
    }
    /// <inheritdoc />
    public override string ToString() => "RecordChangedEvent(redacted)";
}
/// <summary>before Hook 的同事务输入；Operation 为 create/update/delete，租户和主体来自可信宿主。</summary>
public sealed record RecordHookContext(Guid TenantId, RecordActor Actor, string EntityCode, string RecordId,
    string Operation, JsonElement? Before, JsonElement? Proposed)
{
    /// <inheritdoc />
    public override string ToString() => "RecordHookContext(redacted)";
}
/// <summary>before Hook 可以拒绝或替换载荷；执行器须对改写结果重新执行字段权限、校验与业务主键检查。</summary>
public sealed class RecordHookResult
{
    private RecordHookResult(bool allowed, JsonElement? payload, QianError? error)
    {
        Allowed = allowed;
        Payload = payload?.Clone();
        Error = error;
    }
    /// <summary>是否继续执行当前事务。</summary>
    public bool Allowed { get; }
    /// <summary>可选替换对象；null 表示沿用原始 Proposed。</summary>
    public JsonElement? Payload { get; }
    /// <summary>拒绝原因；由宿主映射为统一 ErrorDetail。</summary>
    public QianError? Error { get; }
    /// <summary>允许继续；可给出替换对象，不能以数组或标量替换记录。</summary>
    public static RecordHookResult Accept(JsonElement? payload = null)
    {
        if (payload is { ValueKind: not JsonValueKind.Object }) throw new ArgumentException("记录载荷必须为对象。", nameof(payload));
        return new(true, payload, null);
    }
    /// <summary>拒绝并要求执行器回滚；错误必须来自已登记的组件错误目录。</summary>
    public static RecordHookResult Reject(QianError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(false, null, error);
    }
    /// <inheritdoc />
    public override string ToString() => "RecordHookResult(redacted)";
}
/// <summary>记录扩展端口；S1 交付契约，S2 动态引擎负责事务边界、次序、重试与权限检查。</summary>
public interface IRecordHook
{
    /// <summary>提交前在同一事务中调用；拒绝或异常回滚，允许改写必须重新校验，禁止不可回滚的外部副作用。</summary>
    ValueTask<RecordHookResult> BeforeAsync(RecordHookContext context, CancellationToken cancellationToken = default);
    /// <summary>提交成功后经 Outbox 异步调用；至少一次，必须按 EventId 幂等，失败不撤销已提交业务数据。</summary>
    ValueTask AfterAsync(RecordChangedEvent change, CancellationToken cancellationToken = default);
}
/// <summary>记录事件的显式源生成元数据登记入口。</summary>
public static class RecordEventRegistration
{
    /// <summary>登记记录事件，不启动发布器或数据库。</summary>
    public static IEventTypeRegistrar RegisterRecordEvents(this IEventTypeRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        return registrar.Register(RecordChangedEvent.EventType, DataContractJson.RecordChangedEventTypeInfo);
    }
}
