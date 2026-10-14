using Kun.Core.MultiTenancy;
using Kun.EventBus;
using Qian.Events;
using Qian.Runtime.Errors;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qian.Data;

/// <summary>动态记录规则；主键策略首次保存后不可改，S2 引擎执行这些策略。</summary>
public sealed record DataRulePolicy(string PrimaryKeyStrategy, bool OptimisticConcurrency, bool SoftDelete,
    bool RecordHistory, IReadOnlyList<string> BusinessKey);
/// <summary>可信运行上下文提供的事件主体，与 v0 信封的 Actor 同形。</summary>
public sealed record RecordActor(string Kind, string Id, [property: JsonPropertyName("on_behalf_of")] string? OnBehalfOf = null)
{
    /// <summary>按信封或事件来源的主体换算，载荷的 actor 由此与信封逐字段一致。</summary>
    public static RecordActor From(EventActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var kind = actor.Kind switch
        {
            EventActorKind.User => "user", EventActorKind.Service => "service", EventActorKind.Agent => "agent",
            _ => throw new ArgumentOutOfRangeException(nameof(actor), "未知主体类型。")
        };
        return new(kind, actor.Id, actor.OnBehalfOf);
    }
}
/// <summary>
/// qian.record.changed.v1 的载荷，也是登记到事件目录的集成事件类型：经数据会话的 Outbox 写入端入队，信封由 Qian.Events 生成。
/// 快照必须按字段权限和敏感字段策略裁剪。
/// </summary>
[IntegrationEventId(EventType)]
public sealed record RecordChangedPayload(string EntityCode, string RecordId, string Op, Guid TenantId, RecordActor Actor,
    IReadOnlyList<string> ChangedFields, JsonElement? Before = null, JsonElement? After = null)
{
    /// <summary>稳定事件标识；载荷破坏性变更必须发布新的版本。</summary>
    public const string EventType = "qian.record.changed.v1";
    /// <summary>入队前核对：tenantId 与 actor 必须等于写入端随后写进信封的值，即当前租户上下文与 EventEnvelopeScope 的主体；结构仍须通过发布的 JSON Schema。</summary>
    public void EnsureConsistentContext(ITenantContext tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        var origin = EventEnvelopeScope.Current;
        if (TenantId == Guid.Empty || tenant.TenantId != TenantId.ToString("D") || origin is null || Actor != RecordActor.From(origin.Actor))
            throw new InvalidOperationException("记录事件的租户或主体与当前事件来源不一致。");
    }
    /// <inheritdoc />
    public override string ToString() => "RecordChangedPayload(redacted)";
}
/// <summary>
/// after Hook 的输入：由解析后的 v1 信封与按 RecordChangedPayload 反序列化的载荷派生，只能经 FromEnvelope 构造。
/// 它不是集成事件类型，也不单独序列化；线上只有 Qian.Events 生成的一层信封。
/// </summary>
public sealed class RecordChangedEvent
{
    private RecordChangedEvent(QianEventEnvelope envelope, RecordChangedPayload payload)
    {
        Envelope = envelope;
        Payload = payload;
    }
    /// <summary>收到的信封；producer、actor、trace_parent 与因果链取自这里。</summary>
    public QianEventEnvelope Envelope { get; }
    /// <summary>类型化载荷，租户与主体已和信封核对一致。</summary>
    public RecordChangedPayload Payload { get; }
    /// <summary>信封 event_id，after Hook 的幂等键。</summary>
    public Guid EventId => Envelope.EventId;
    /// <summary>记录所属租户，等于信封 tenant_id 与载荷 tenantId。</summary>
    public Guid TenantId => Payload.TenantId;
    /// <summary>从信封派生；type 不符、平台级信封、载荷不符合契约，或载荷的租户与主体和信封不一致时拒绝。</summary>
    public static RecordChangedEvent FromEnvelope(QianEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        envelope.Validate();
        if (envelope.Type != RecordChangedPayload.EventType) throw new InvalidOperationException("信封不是 " + RecordChangedPayload.EventType + " 事件。");
        RecordChangedPayload? payload;
        try { payload = envelope.Payload.Deserialize(DataContractJson.RecordChangedPayloadTypeInfo); }
        catch (JsonException error) { throw new InvalidOperationException("记录事件载荷不符合 RecordChangedPayload 契约。", error); }
        if (payload is not { EntityCode: not null, RecordId: not null, Op: not null, ChangedFields: not null })
            throw new InvalidOperationException("记录事件载荷不符合 RecordChangedPayload 契约。");
        if (envelope.TenantId is not { } tenantId || payload.TenantId != tenantId || payload.Actor != RecordActor.From(envelope.Actor))
            throw new InvalidOperationException("记录事件载荷的租户或主体与信封不一致。");
        return new(envelope, payload);
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
    /// <summary>提交成功后经 Outbox 异步调用，输入由 RecordChangedEvent.FromEnvelope 从收到的信封派生；至少一次，必须按 EventId 幂等，失败不撤销已提交业务数据。</summary>
    ValueTask AfterAsync(RecordChangedEvent change, CancellationToken cancellationToken = default);
}
/// <summary>记录事件的显式源生成元数据登记入口。</summary>
public static class RecordEventRegistration
{
    /// <summary>以载荷类型及其源生成元数据登记 qian.record.changed.v1，不启动发布器或数据库；信封由 Qian.Events 写入端生成。</summary>
    public static IEventTypeRegistrar RegisterRecordEvents(this IEventTypeRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        return registrar.Register(RecordChangedPayload.EventType, DataContractJson.RecordChangedPayloadTypeInfo);
    }
}
