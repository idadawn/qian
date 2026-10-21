using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors;

namespace Qian.Identity.Server;

/// <summary>
/// 登录标识的规范化、类型判定与掩码。三类标识的语法互不重叠：邮箱含 @，手机只含数字与加号，
/// 用户名必须字母开头且不含 @，因此一个输入串只可能属于一类，登录无需用户选择类型。
/// </summary>
internal static partial class IdentifierRules
{
    internal const int MaxLength = 320;

    /// <summary>按语法判定输入串的类型并规范化；格式非法返回 null。</summary>
    internal static (IdentifierKind Kind, string Value)? Classify(string? input, string defaultCountryCode)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > MaxLength) return null;
        var text = input.Normalize(NormalizationForm.FormKC).Trim();
        if (text.Contains('@', StringComparison.Ordinal)) return Normalize(IdentifierKind.Email, text, defaultCountryCode) is { } email ? (IdentifierKind.Email, email) : null;
        if (text.Length > 0 && (text[0] == '+' || char.IsAsciiDigit(text[0])))
            return Normalize(IdentifierKind.Phone, text, defaultCountryCode) is { } phone ? (IdentifierKind.Phone, phone) : null;
        return Normalize(IdentifierKind.Username, text, defaultCountryCode) is { } user ? (IdentifierKind.Username, user) : null;
    }

    /// <summary>把给定类型的原值规范化；不合法返回 null。邮箱与用户名转小写，手机号补国家码为 E.164。</summary>
    internal static string? Normalize(IdentifierKind kind, string? raw, string defaultCountryCode)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > MaxLength) return null;
        var text = raw.Normalize(NormalizationForm.FormKC).Trim();
        switch (kind)
        {
            case IdentifierKind.Email:
                var email = text.ToLowerInvariant();
                return email.Length <= 254 && EmailPattern().IsMatch(email) ? email : null;
            case IdentifierKind.Username:
                var user = text.ToLowerInvariant();
                return UsernamePattern().IsMatch(user) ? user : null;
            default:
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
                    // 国内写法：去掉主叫前缀 0；已写了国家码（长度足够）的不重复补。
                    var national = value.TrimStart('0');
                    value = national.StartsWith(defaultCountryCode, StringComparison.Ordinal) && national.Length >= defaultCountryCode.Length + 7
                        ? "+" + national : "+" + defaultCountryCode + national;
                }
                return PhonePattern().IsMatch(value) ? value : null;
        }
    }

    internal static string KindName(IdentifierKind kind) => kind switch { IdentifierKind.Phone => "phone", IdentifierKind.Email => "email", _ => "username" };

    internal static IdentifierKind ParseKind(string name) => name switch
    {
        "phone" => IdentifierKind.Phone, "email" => IdentifierKind.Email, "username" => IdentifierKind.Username,
        _ => throw new InvalidOperationException("未知标识类型。")
    };

    /// <summary>对外与审计使用的掩码；原值不出身份组件。</summary>
    internal static string Mask(IdentifierKind kind, string value)
    {
        switch (kind)
        {
            case IdentifierKind.Email:
                var at = value.LastIndexOf('@');
                return at <= 0 ? "***" : value[0] + "***" + value[at..];
            case IdentifierKind.Phone:
                var digits = value.TrimStart('+');
                if (digits.Length >= 11) return digits[^11..^8] + "****" + digits[^4..];
                return digits.Length >= 6 ? digits[..2] + "***" + digits[^2..] : "***";
            default:
                return value.Length <= 3 ? value[0] + "**" : value[..2] + "***";
        }
    }

    /// <summary>登录审计与日志使用的掩码；无法判定类型的输入只留长度。</summary>
    internal static (string? Kind, string? Masked) MaskInput(string? input, string defaultCountryCode)
    {
        if (string.IsNullOrWhiteSpace(input)) return (null, null);
        return Classify(input, defaultCountryCode) is { } classified
            ? (KindName(classified.Kind), Mask(classified.Kind, classified.Value))
            : ("invalid", "len=" + Math.Min(input.Length, 999).ToString(CultureInfo.InvariantCulture));
    }

    // 仅做语法层面的合理性检查：邮箱可达性靠验证，不靠正则。
    [GeneratedRegex(@"\A[a-z0-9._%+\-]+@[a-z0-9]([a-z0-9\-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9\-]*[a-z0-9])?)+\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex EmailPattern();
    [GeneratedRegex(@"\A[a-z][a-z0-9_.\-]{2,31}\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex UsernamePattern();
    [GeneratedRegex(@"\A\+[1-9][0-9]{6,14}\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex PhonePattern();

    /// <summary>输入非法时的字段错误，不回显原值。</summary>
    internal static QianErrorException Invalid(string pointer) =>
        new(new QianError(PlatformErrors.InputInvalid) { Errors = [new QianFieldError(PlatformErrors.PatternMismatch, pointer)] });
}
