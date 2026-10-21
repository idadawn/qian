using System.Security.Cryptography;
using System.Text;
using Qian.Identity.Abstractions;

namespace Qian.Identity.Server;

/// <summary>
/// 从临时认证材料取出的密码：UTF-8 字节，已做 NFKC 规范化（全角与半角字母数字、组合与分解字符视为同一密码），
/// 释放时清零。规范化只发生在这里，哈希与校验两侧共用，避免输入法差异让用户被锁在门外。
/// </summary>
internal readonly struct PasswordSecret : IDisposable
{
    private PasswordSecret(byte[] bytes, int length, bool edgeWhitespace, bool control) { Bytes = bytes; Length = length; HasEdgeWhitespace = edgeWhitespace; HasControl = control; }

    internal byte[] Bytes { get; }
    /// <summary>规范化后的字符数（按 UTF-16 码元计，用于长度策略）。</summary>
    internal int Length { get; }
    internal bool HasEdgeWhitespace { get; }
    internal bool HasControl { get; }

    internal static PasswordSecret From(AuthenticationProof proof)
    {
        ArgumentNullException.ThrowIfNull(proof);
        // 材料可能不是合法 UTF-8；无效序列会被替换为 U+FFFD，后续只会让校验失败，不会抛出。
        var text = Encoding.UTF8.GetString(proof.Data.Span).Normalize(NormalizationForm.FormKC);
        var bytes = Encoding.UTF8.GetBytes(text);
        return new PasswordSecret(bytes, text.Length, text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1])), text.Any(char.IsControl));
    }

    public void Dispose() => CryptographicOperations.ZeroMemory(Bytes);
}
