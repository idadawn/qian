using System.Text.Json;

namespace Qian.Audit;

/// <summary>既有输入审计策略入口；脱敏协议由共享契约层唯一实现。</summary>
public sealed class AuditInputPolicy
{
    private readonly AuditInputProjection _projection;
    /// <summary>敏感值的固定标记，不包含长度、类型或原值哈希。</summary>
    public const string Redacted = AuditInputProjection.Redacted;
    /// <summary>保持既有构造和默认审计行为，不改变旧消费者策略。</summary>
    public AuditInputPolicy(JsonElement schema, bool isCommand, bool? audit = null) => _projection = new AuditInputProjection(schema, isCommand, audit);
    /// <summary>是否记录此次调用。</summary>
    public bool ShouldRecord => _projection.ShouldRecord;
    /// <summary>计算脱敏输入哈希；关闭审计时返回 null。</summary>
    public string? InputHash(JsonElement input) => _projection.InputHash(input);
    /// <summary>生成脱敏投影，不修改输入。</summary>
    public JsonElement Redact(JsonElement input) => _projection.Redact(input);
    internal static System.Text.RegularExpressions.Regex Pattern(string pattern) => AuditInputProjection.Pattern(pattern);
}
