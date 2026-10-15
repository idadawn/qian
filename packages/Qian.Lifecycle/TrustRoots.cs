using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Qian.Config;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Lifecycle;

// 平台信任根（PRD 7.2.1 / 7.17）：Apollo 公共 namespace qian.common.trust 的 Trust:PlatformKeys 节逐项给出公钥的 secretRef 与有效期，
// 公钥本身在 K8s Secret。每次验签重新读取配置与秘密，轮换不重启；有效期重叠时新旧公钥同时可信。
internal sealed class TrustRoots(IPluginContext context, TimeProvider time)
{
    internal const string Section = "Trust:PlatformKeys";
    // Ed25519 公钥的 SubjectPublicKeyInfo（DER）前缀：SEQUENCE { SEQUENCE { OID 1.3.101.112 }, BIT STRING }，其后是 32 字节公钥。
    private static readonly byte[] SpkiPrefix = Convert.FromHexString("302a300506032b6570032100");
    private static readonly System.Buffers.SearchValues<char> LowerHex = System.Buffers.SearchValues.Create("0123456789abcdef");

    // 返回验签公钥的指纹；未命中当前有效的信任根、签名格式不对或验签失败一律 LIFECYCLE_SIGNATURE_INVALID，不区分原因。
    internal async ValueTask<string> VerifyAsync(string digest, ArtifactSignature signature, CancellationToken ct)
    {
        if (!Fingerprint(signature.KeyId) || signature.Value.Length != 64) throw Invalid();
        var message = Convert.FromHexString(digest["sha256:".Length..]);
        var now = time.GetUtcNow();
        foreach (var root in Roots(context.Config().ReadSection(Section)))
        {
            if (root.NotBefore is { } from && now < from || root.NotAfter is { } until && now >= until) continue;
            byte[] spki;
            try
            {
                using var secret = await context.ResolveSecretAsync(root.PublicKey, ct).ConfigureAwait(false);
                spki = ReadPublicKey(secret.RevealUtf8());
            }
            catch (Exception error) when (error is QianErrorException or FormatException or InvalidOperationException or CryptographicException)
            {
                // 单把公钥无法读取只让它不可信，不影响其它信任根；原因进日志，不带引用之外的内容。
                LifecycleLog.TrustRootUnreadable(context.Logger, root.PublicKey);
                continue;
            }
            if ("sha256:" + Convert.ToHexStringLower(SHA256.HashData(spki)) != signature.KeyId) continue;
            var verifier = new Ed25519Signer();
            verifier.Init(false, new Ed25519PublicKeyParameters(spki, SpkiPrefix.Length));
            verifier.BlockUpdate(message, 0, message.Length);
            return verifier.VerifySignature(signature.Value.ToArray()) ? signature.KeyId : throw Invalid();
        }
        throw Invalid();
    }

    // 配置节的相对键形如 0:PublicKey / 0:NotBefore / 0:NotAfter；缺有效期表示不设该端的界限，时间不合法的条目整条跳过。
    private static IEnumerable<TrustRoot> Roots(IReadOnlyDictionary<string, string?> section)
    {
        foreach (var group in section.Where(item => item.Key.Contains(':', StringComparison.Ordinal))
            .GroupBy(item => item.Key[..item.Key.IndexOf(':', StringComparison.Ordinal)], StringComparer.Ordinal)
            .OrderBy(group => int.TryParse(group.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ? index : int.MaxValue))
        {
            var values = group.ToDictionary(item => item.Key[(group.Key.Length + 1)..], item => item.Value, StringComparer.OrdinalIgnoreCase);
            if (!values.TryGetValue("PublicKey", out var reference) || reference is null || !SecretReference.IsValid(reference)) continue;
            if (!Time(values, "NotBefore", out var notBefore) || !Time(values, "NotAfter", out var notAfter)) continue;
            yield return new TrustRoot(reference, notBefore, notAfter);
        }
    }

    private static bool Time(Dictionary<string, string?> values, string key, out DateTimeOffset? value)
    {
        value = null;
        if (!values.TryGetValue(key, out var text) || string.IsNullOrWhiteSpace(text)) return true;
        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)) return false;
        value = parsed;
        return true;
    }

    // 只接受 PEM 包装的 Ed25519 SubjectPublicKeyInfo，与 CLI 市场信任根同一格式与指纹算法。
    internal static byte[] ReadPublicKey(string pem)
    {
        var text = pem.Trim();
        const string Begin = "-----BEGIN PUBLIC KEY-----", End = "-----END PUBLIC KEY-----";
        if (!text.StartsWith(Begin, StringComparison.Ordinal) || !text.EndsWith(End, StringComparison.Ordinal)) throw new FormatException("公钥必须是 PEM。");
        var body = new StringBuilder();
        foreach (var c in text[Begin.Length..^End.Length]) if (!char.IsWhiteSpace(c)) body.Append(c);
        var der = Convert.FromBase64String(body.ToString());
        if (der.Length != SpkiPrefix.Length + 32 || !der.AsSpan(0, SpkiPrefix.Length).SequenceEqual(SpkiPrefix)) throw new FormatException("公钥不是 Ed25519。");
        return der;
    }

    internal static bool Fingerprint(string? value) => value is { Length: 71 } && value.StartsWith("sha256:", StringComparison.Ordinal) &&
        value.AsSpan(7).IndexOfAnyExcept(LowerHex) < 0;

    private static QianErrorException Invalid() => new(new QianError(LifecycleErrors.SignatureInvalid));

    private sealed record TrustRoot(string PublicKey, DateTimeOffset? NotBefore, DateTimeOffset? NotAfter);
}
