using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Qian.Runtime.Errors;

namespace Qian.Tenancy.Signup;

// 输入规范化、掩码与秘密材料。手机号的规范化与掩码与身份服务逐条相同（Qian.Identity.Server 的 IdentifierRules），
// 这样以手机号查到的自然人、建账号时绑定的标识、短信发往的号码是同一个值。
internal static partial class SignupRules
{
    /// <summary>规范化为 E.164；不合法返回 null。去掉空格、连字符、括号与点，00 前缀换成 +，没有国家码时去掉主叫前缀 0 再补缺省国家码。</summary>
    internal static string? NormalizePhone(string? raw, string defaultCountryCode)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 64) return null;
        var text = raw.Normalize(NormalizationForm.FormKC).Trim();
        var digits = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsAsciiDigit(c)) digits.Append(c);
            else if (c == '+' && i == 0) digits.Append(c);
            else if (c is ' ' or '-' or '(' or ')' or '.') continue;
            else return null;
        }
        var value = digits.ToString();
        if (value.Length == 0) return null;
        if (value.StartsWith("00", StringComparison.Ordinal)) value = "+" + value[2..];
        else if (value[0] != '+')
        {
            var national = value.TrimStart('0');
            value = national.StartsWith(defaultCountryCode, StringComparison.Ordinal) && national.Length >= defaultCountryCode.Length + 7
                ? "+" + national : "+" + defaultCountryCode + national;
        }
        return PhonePattern().IsMatch(value) ? value : null;
    }

    /// <summary>对外的手机号掩码（如 <c>139****5678</c>），与身份服务的掩码一致。</summary>
    internal static string MaskPhone(string value)
    {
        var digits = value.TrimStart('+');
        if (digits.Length >= 11) return digits[^11..^8] + "****" + digits[^4..];
        return digits.Length >= 6 ? digits[..2] + "***" + digits[^2..] : "***";
    }

    /// <summary>租户号的可用性原因：格式或长度不合法为 invalid，保留字为 reserved，否则 null（是否被占用另查）。</summary>
    internal static string? CodeProblem(string? code, TenancySignupOptions options)
    {
        if (string.IsNullOrEmpty(code) || code.Length < options.CodeMinLength || code.Length > options.CodeMaxLength || !CodePattern().IsMatch(code)) return SignupCodeReasons.Invalid;
        return options.Reserved.Contains(code) ? SignupCodeReasons.Reserved : null;
    }

    /// <summary>邮箱规范化为小写并做语法检查（与身份服务同一规则）；不合法返回 null。</summary>
    internal static string? NormalizeEmail(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var email = raw.Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant();
        return email.Length <= 254 && EmailPattern().IsMatch(email) ? email : null;
    }

    /// <summary>名称类字段：去掉首尾空白后非空、不超长、不含控制字符（与租户名称的规则一致）。</summary>
    internal static bool IsName(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength && !value.Any(char.IsControl);

    /// <summary>自由文本（申请说明等）：可含换行与制表符，不含其它控制字符。</summary>
    internal static bool IsText(string? value, int maxLength) =>
        value is null || (value.Length <= maxLength && !value.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')));

    /// <summary>新验证码：定长十进制数字，均匀分布。</summary>
    internal static string NewCode(int length) =>
        RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, length)).ToString("D" + length.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    /// <summary>验证码的加盐哈希：HMAC-SHA256(盐, 验证码)。</summary>
    internal static byte[] HashCode(byte[] salt, string code) => HMACSHA256.HashData(salt, Encoding.UTF8.GetBytes(code));

    /// <summary>核对验证码；长度不同也走完整比较，不提前返回。</summary>
    internal static bool CodeMatches(byte[] salt, byte[] hash, string code) => CryptographicOperations.FixedTimeEquals(HashCode(salt, code), hash);

    /// <summary>新的申请人会话令牌：32 字节随机数的 base64url，只交给宿主写进 HttpOnly Cookie。</summary>
    internal static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>会话令牌的存储形式：SHA-256。格式不对的令牌返回 null（直接当作无效会话）。</summary>
    internal static byte[]? HashToken(string? token) =>
        token is { Length: 43 } && TokenPattern().IsMatch(token) ? SHA256.HashData(Encoding.ASCII.GetBytes(token)) : null;

    /// <summary>申请编号 AP-yyyyMMdd-NNNN（UTC 日期，同日序号）。</summary>
    internal static string Number(DateTimeOffset day, int sequence) =>
        "AP-" + day.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-" + sequence.ToString("D4", CultureInfo.InvariantCulture);

    internal static QianErrorException Invalid(ErrorCode field, string pointer) =>
        new(new QianError(PlatformErrors.InputInvalid) { Errors = [new QianFieldError(field, pointer)] });

    [GeneratedRegex(@"\A\+[1-9][0-9]{6,14}\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex PhonePattern();
    [GeneratedRegex(@"\A[a-z0-9]+\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex CodePattern();
    [GeneratedRegex(@"\A[a-z0-9._%+\-]+@[a-z0-9]([a-z0-9\-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9\-]*[a-z0-9])?)+\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex EmailPattern();
    [GeneratedRegex(@"\A[A-Za-z0-9_\-]{43}\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex TokenPattern();
}
