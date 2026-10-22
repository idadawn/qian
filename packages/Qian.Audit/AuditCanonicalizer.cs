using System.Text.Json;

namespace Qian.Audit;

/// <summary>兼容既有公开入口；唯一 v1 规范化与哈希实现由共享契约层拥有。</summary>
public static class AuditCanonicalizer
{
    /// <summary>计算包含全部审计字段的 v1 哈希，保持历史字节协议。</summary>
    public static string Hash(AuditRecord record, long sequence, string? previousHash) => AuditProtocol.Hash(record, sequence, previousHash);
    internal static string Content(AuditRecord record, long sequence) => AuditProtocol.Content(record, sequence);
    /// <summary>确定性 JSON 编码，转发到唯一共享实现。</summary>
    public static string Canonicalize(JsonElement value) => AuditProtocol.Canonicalize(value);
}
