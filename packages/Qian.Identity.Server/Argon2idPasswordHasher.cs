using System.Globalization;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

namespace Qian.Identity.Server;

/// <summary>
/// Argon2id 密码哈希（RFC 9106），输出 PHC 字符串 <c>$argon2id$v=19$m=…,t=…,p=…$盐$摘要</c>。
/// 成本参数随哈希存储，所以调高成本后旧哈希仍可验证，并在下次成功登录时按新参数重新哈希。
/// </summary>
internal sealed class Argon2idPasswordHasher
{
    // 校验时拒绝库里不合理的成本参数，防止被篡改的哈希行拖垮进程。
    private const int MaxMemoryKiB = 1024 * 1024, MaxIterations = 16, MaxParallelism = 16;
    private readonly Argon2idOptions _options;
    private readonly string _dummy;

    internal Argon2idPasswordHasher(Argon2idOptions options)
    {
        ArgumentNullException.ThrowIfNull(options); options.Validate(); _options = options;
        _dummy = Hash("qian-identity-dummy-credential"u8);
    }

    internal string Hash(ReadOnlySpan<byte> password)
    {
        var salt = RandomNumberGenerator.GetBytes(_options.SaltBytes);
        var digest = Compute(password, salt, _options.MemoryKiB, _options.Iterations, _options.Parallelism, _options.HashBytes);
        try { return $"$argon2id$v=19$m={_options.MemoryKiB.ToString(CultureInfo.InvariantCulture)},t={_options.Iterations.ToString(CultureInfo.InvariantCulture)},p={_options.Parallelism.ToString(CultureInfo.InvariantCulture)}${Encode(salt)}${Encode(digest)}"; }
        finally { CryptographicOperations.ZeroMemory(digest); }
    }

    /// <summary>常量时间比较；格式或参数不合法的哈希一律视为不匹配。</summary>
    internal static bool Verify(string encoded, ReadOnlySpan<byte> password)
    {
        if (!TryParse(encoded, out var memory, out var iterations, out var parallelism, out var salt, out var expected)) return false;
        var actual = Compute(password, salt, memory, iterations, parallelism, expected.Length);
        try { return CryptographicOperations.FixedTimeEquals(actual, expected); }
        finally { CryptographicOperations.ZeroMemory(actual); }
    }

    /// <summary>账号不存在或没有密码时做一次等价成本的哈希，使响应时间不泄露账号是否存在。</summary>
    internal void BurnDummy(ReadOnlySpan<byte> password) => Verify(_dummy, password);

    /// <summary>已存哈希的成本低于当前配置，或不是当前格式时，应在登录成功后重新哈希。</summary>
    internal bool NeedsRehash(string encoded) =>
        !TryParse(encoded, out var memory, out var iterations, out var parallelism, out var salt, out var digest) ||
        memory < _options.MemoryKiB || iterations < _options.Iterations || parallelism != _options.Parallelism || salt.Length < _options.SaltBytes || digest.Length < _options.HashBytes;

    private static byte[] Compute(ReadOnlySpan<byte> password, byte[] salt, int memoryKiB, int iterations, int parallelism, int length)
    {
        var parameters = new Argon2Parameters.Builder(Argon2Parameters.Argon2id).WithVersion(Argon2Parameters.Version13)
            .WithIterations(iterations).WithMemoryAsKB(memoryKiB).WithParallelism(parallelism).WithSalt(salt).Build();
        var generator = new Argon2BytesGenerator(); generator.Init(parameters);
        var secret = password.ToArray(); var digest = new byte[length];
        try { generator.GenerateBytes(secret, digest); return digest; }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }

    private static bool TryParse(string encoded, out int memory, out int iterations, out int parallelism, out byte[] salt, out byte[] digest)
    {
        memory = iterations = parallelism = 0; salt = digest = [];
        var parts = encoded.Split('$');
        if (parts.Length != 6 || parts[0].Length != 0 || parts[1] != "argon2id" || parts[2] != "v=19") return false;
        var costs = parts[3].Split(',');
        if (costs.Length != 3 || !TryCost(costs[0], "m=", out memory) || !TryCost(costs[1], "t=", out iterations) || !TryCost(costs[2], "p=", out parallelism)) return false;
        if (memory < 8 * parallelism || memory > MaxMemoryKiB || iterations > MaxIterations || parallelism > MaxParallelism) return false;
        try { salt = Decode(parts[4]); digest = Decode(parts[5]); }
        catch (FormatException) { return false; }
        return salt.Length >= 8 && digest.Length >= 16;
    }

    private static bool TryCost(string text, string prefix, out int value)
    {
        value = 0;
        return text.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(text.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;
    }

    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=');

    private static byte[] Decode(string value) => Convert.FromBase64String(value.PadRight(value.Length + (4 - value.Length % 4) % 4, '='));
}
