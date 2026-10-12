using System.Text.Json;
using Qian.Runtime.Errors;

namespace Qian.Audit;

/// <summary>进入审计链的不可变记录；输入只保留脱敏后的哈希，Payload 须由可信调用方按审计契约构造。</summary>
public sealed class AuditRecord
{
    /// <summary>构造已校验的记录；时间规整到 PostgreSQL 微秒精度。平台记录可表示，但不因此获得平台写权限。</summary>
    public AuditRecord(Guid id, string? tenantId, string actorType, string actorId, string? onBehalfOf,
        string capabilityId, string contractVersion, string inputHash, string channel, string? resource,
        string result, string traceId, DateTimeOffset occurredAt, JsonElement payload)
    {
        if (id == Guid.Empty || (tenantId is not null && (!Guid.TryParseExact(tenantId, "D", out var parsed) || parsed == Guid.Empty)) ||
            actorType is not ("user" or "service" or "agent") || channel is not ("ui" or "api" or "mcp" or "a2a" or "job" or "event") ||
            !Hex(inputHash, 64) || !Hex(traceId, 32) || payload.ValueKind != JsonValueKind.Object || payload.GetRawText().Length > 65536)
            throw Invalid();
        Id = id; TenantId = tenantId; ActorType = actorType; ActorId = Text(actorId, 200);
        OnBehalfOf = onBehalfOf is null ? null : Text(onBehalfOf, 200); CapabilityId = Text(capabilityId, 384);
        ContractVersion = Text(contractVersion, 100); InputHash = inputHash; Channel = channel;
        Resource = resource is null ? null : Text(resource, 512); Result = Text(result, 64); TraceId = traceId;
        OccurredAt = new DateTimeOffset(occurredAt.UtcTicks - occurredAt.UtcTicks % 10, TimeSpan.Zero); Payload = payload.Clone();
    }
    /// <summary>全局事件实例 ID，写入重试复用。</summary>
    public Guid Id { get; }
    /// <summary>租户 UUID；平台级为 null。</summary>
    public string? TenantId { get; }
    /// <summary>user / service / agent。</summary>
    public string ActorType { get; }
    /// <summary>执行主体 ID。</summary>
    public string ActorId { get; }
    /// <summary>代行的用户 ID。</summary>
    public string? OnBehalfOf { get; }
    /// <summary>稳定能力 ID。</summary>
    public string CapabilityId { get; }
    /// <summary>能力契约版本。</summary>
    public string ContractVersion { get; }
    /// <summary>脱敏后的输入哈希。</summary>
    public string InputHash { get; }
    /// <summary>调用通道。</summary>
    public string Channel { get; }
    /// <summary>资源引用。</summary>
    public string? Resource { get; }
    /// <summary>执行结果。</summary>
    public string Result { get; }
    /// <summary>W3C 追踪标识。</summary>
    public string TraceId { get; }
    /// <summary>已规整为 UTC 微秒精度的发生时间。</summary>
    public DateTimeOffset OccurredAt { get; }
    /// <summary>按能力审计契约构造的补充元数据，不能直接传原始输入。</summary>
    public JsonElement Payload { get; }
    /// <inheritdoc />
    public override string ToString() => "AuditRecord(redacted)";
    private static string Text(string value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max && !value.Any(char.IsControl) ? value : throw Invalid();
    private static bool Hex(string value, int length) => value is not null && value.Length == length && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static QianErrorException Invalid() => new(new QianError(PlatformErrors.InputInvalid));
}

/// <summary>持久化的链节点；序号和前序都参与哈希验证。</summary>
public sealed record AuditChainEntry(long Sequence, string? PreviousHash, string ContentHash, AuditRecord Record)
{
    /// <inheritdoc />
    public override string ToString() => $"AuditChainEntry(sequence={Sequence}, redacted)";
}

/// <summary>全链验证结果；BrokenAt 指出最早不一致的位置，不回显敏感记录。</summary>
public sealed record AuditChainVerification(bool Valid, long CheckedEntries, long? BrokenAt, string? Reason);
