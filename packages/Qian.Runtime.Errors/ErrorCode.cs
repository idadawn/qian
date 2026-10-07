using System.Globalization;
using System.Text.RegularExpressions;

namespace Qian.Runtime.Errors;

/// <summary>
/// 一个登记在错误码目录里的稳定错误码：类别、状态码、归属组件与多语言文案。
/// 组件在自己的 Definition 包里以 <c>static readonly</c> 字段定义，挂载时登记到 <see cref="ErrorCatalog"/>。
/// </summary>
/// <remarks>
/// 文案以 BCP 47 语言标签为键，<c>zh-CN</c> 必填；文案模板里的 <c>{name}</c> 是占位符，
/// 各语言的占位符集合必须相同，出错方按名字提供参数（<see cref="QianError"/>）。
/// </remarks>
public sealed partial class ErrorCode : IEquatable<ErrorCode>
{
    /// <summary>必填的默认语言。</summary>
    public const string DefaultLocale = "zh-CN";

    /// <summary>问题类型 URI 的前缀：<c>https://qian.dev/problems/&lt;kind&gt;/&lt;code&gt;</c>。</summary>
    public const string TypeBaseUri = "https://qian.dev/problems/";

    private readonly SortedDictionary<string, string> _title;
    private readonly SortedDictionary<string, string> _message;

    private ErrorCode(
        string code,
        ErrorKind kind,
        int? status,
        string owner,
        bool retryable,
        IReadOnlyDictionary<string, string> title,
        IReadOnlyDictionary<string, string> message)
    {
        if (code is null || !CodePattern().IsMatch(code) || code.Length > 64)
            throw new ArgumentException($"错误码 '{code}' 不合法：须为大写下划线（^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$），最长 64。", nameof(code));
        if (owner is null || !OwnerPattern().IsMatch(owner))
            throw new ArgumentException($"归属组件 id '{owner}' 不合法：须为反向域名风格的组件 id，如 qian.files。", nameof(owner));

        _title = CopyText(title, nameof(title), code);
        _message = CopyText(message, nameof(message), code);

        Code = code;
        Kind = kind;
        Status = status;
        Owner = owner;
        Retryable = retryable;
        Type = kind == ErrorKind.Field ? null : TypeBaseUri + KindName(kind) + "/" + code;
        Parameters = ExtractParameters(_message, code, nameof(message));
    }

    /// <summary>稳定错误码（大写下划线），全局唯一。</summary>
    public string Code { get; }

    /// <summary>类别。</summary>
    public ErrorKind Kind { get; }

    /// <summary>HTTP 状态码；<see cref="ErrorKind.Field"/> 没有状态码。</summary>
    public int? Status { get; }

    /// <summary>登记该错误码的组件 id。</summary>
    public string Owner { get; }

    /// <summary>以同一幂等键原样重试是否可能成功。</summary>
    public bool Retryable { get; }

    /// <summary>问题类型 URI；<see cref="ErrorKind.Field"/> 为 null。</summary>
    public string? Type { get; }

    /// <summary>标题：语言标签 → 文本（按语言标签排序）。</summary>
    public IReadOnlyDictionary<string, string> Title => _title;

    /// <summary>文案模板：语言标签 → 模板（按语言标签排序）。</summary>
    public IReadOnlyDictionary<string, string> Message => _message;

    /// <summary>文案模板里的占位符名（按名称排序）。</summary>
    public IReadOnlyList<string> Parameters { get; }

    /// <summary>定义协议错误码。状态码为 4xx / 5xx，且不是 409 / 422。</summary>
    public static ErrorCode Protocol(
        string code,
        int status,
        string owner,
        IReadOnlyDictionary<string, string> title,
        IReadOnlyDictionary<string, string> message,
        bool retryable = false)
    {
        if (status is < 400 or > 599 || IsBusinessStatus(status))
            throw new ArgumentOutOfRangeException(nameof(status), status, "协议错误的状态码须为 4xx / 5xx，且不能是业务错误专用的 409 / 422。");
        return new ErrorCode(code, ErrorKind.Protocol, status, owner, retryable, title, message);
    }

    /// <summary>定义业务错误码。状态码只能是 409（状态冲突）或 422（业务规则不满足）。</summary>
    public static ErrorCode Business(
        string code,
        int status,
        string owner,
        IReadOnlyDictionary<string, string> title,
        IReadOnlyDictionary<string, string> message,
        bool retryable = false)
    {
        if (!IsBusinessStatus(status))
            throw new ArgumentOutOfRangeException(nameof(status), status, "业务错误的状态码只能是 409 或 422。");
        return new ErrorCode(code, ErrorKind.Business, status, owner, retryable, title, message);
    }

    /// <summary>定义字段级错误码（只用于 <c>errors[]</c>）。</summary>
    public static ErrorCode Field(
        string code,
        string owner,
        IReadOnlyDictionary<string, string> title,
        IReadOnlyDictionary<string, string> message)
        => new(code, ErrorKind.Field, null, owner, false, title, message);

    /// <summary>业务错误专用的状态码：409、422。</summary>
    public static bool IsBusinessStatus(int status) => status is 409 or 422;

    /// <summary>按语言取标题：精确匹配 → 逐级父语言（en-US → en）→ zh-CN。</summary>
    public string GetTitle(CultureInfo? culture = null) => Resolve(_title, culture, out _);

    /// <summary>该语言实际取用的文案语言标签（用于 <c>Content-Language</c>），规则同 <see cref="GetTitle"/>。</summary>
    public string ResolveLocale(CultureInfo? culture = null)
    {
        Resolve(_message, culture, out var locale);
        return locale;
    }

    /// <summary>按语言渲染文案；<paramref name="args"/> 必须覆盖 <see cref="Parameters"/>。</summary>
    /// <exception cref="ArgumentException">缺少占位符参数。</exception>
    public string FormatMessage(CultureInfo? culture = null, IReadOnlyDictionary<string, string>? args = null)
        => FormatMessage(culture, args, out _);

    internal string FormatMessage(CultureInfo? culture, IReadOnlyDictionary<string, string>? args, out string locale)
    {
        EnsureArguments(args);
        var template = Resolve(_message, culture, out locale);
        if (Parameters.Count == 0)
            return template;
        return PlaceholderPattern().Replace(template, m => args![m.Groups[1].Value]);
    }

    /// <summary>检查参数是否覆盖全部占位符。</summary>
    /// <exception cref="ArgumentException">缺少占位符参数。</exception>
    public void EnsureArguments(IReadOnlyDictionary<string, string>? args)
    {
        foreach (var name in Parameters)
        {
            if (args is null || !args.ContainsKey(name))
                throw new ArgumentException($"错误码 {Code} 的文案需要参数 '{name}'。", nameof(args));
        }
    }

    /// <inheritdoc />
    public bool Equals(ErrorCode? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        return other is not null
            && Code == other.Code
            && Kind == other.Kind
            && Status == other.Status
            && Owner == other.Owner
            && Retryable == other.Retryable
            && SameText(_title, other._title)
            && SameText(_message, other._message);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ErrorCode);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Code, Kind, Status, Owner);

    /// <inheritdoc />
    public override string ToString() => Code;

    internal static string KindName(ErrorKind kind) => kind switch
    {
        ErrorKind.Protocol => "protocol",
        ErrorKind.Business => "business",
        _ => "field",
    };

    internal static string Resolve(SortedDictionary<string, string> text, CultureInfo? culture, out string locale)
    {
        for (var c = culture ?? CultureInfo.CurrentUICulture; !string.IsNullOrEmpty(c.Name); c = c.Parent)
        {
            if (text.TryGetValue(c.Name, out var value))
            {
                locale = c.Name;
                return value;
            }
        }
        locale = DefaultLocale;
        return text[DefaultLocale];
    }

    internal SortedDictionary<string, string> TitleText => _title;

    internal SortedDictionary<string, string> MessageText => _message;

    private static SortedDictionary<string, string> CopyText(IReadOnlyDictionary<string, string> text, string paramName, string code)
    {
        ArgumentNullException.ThrowIfNull(text, paramName);
        var copy = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (locale, value) in text)
        {
            if (locale is null || !LocalePattern().IsMatch(locale))
                throw new ArgumentException($"错误码 {code} 的语言标签 '{locale}' 不合法（BCP 47，如 zh-CN、en）。", paramName);
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"错误码 {code} 的 {locale} 文本为空。", paramName);
            copy.Add(locale, value);
        }
        if (!copy.ContainsKey(DefaultLocale))
            throw new ArgumentException($"错误码 {code} 缺少 {DefaultLocale} 文本。", paramName);
        return copy;
    }

    private static string[] ExtractParameters(SortedDictionary<string, string> templates, string code, string paramName)
    {
        string[]? expected = null;
        string? expectedLocale = null;
        foreach (var (locale, template) in templates)
        {
            var names = PlaceholderPattern().Matches(template)
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (expected is null)
            {
                expected = names;
                expectedLocale = locale;
            }
            else if (!expected.SequenceEqual(names, StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    $"错误码 {code} 的文案占位符在 {expectedLocale}（{Join(expected)}）与 {locale}（{Join(names)}）之间不一致。",
                    paramName);
            }
        }
        return expected ?? [];
    }

    private static string Join(string[] names) => names.Length == 0 ? "无" : string.Join(", ", names);

    private static bool SameText(SortedDictionary<string, string> a, SortedDictionary<string, string> b)
        => a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && v == kv.Value);

    [GeneratedRegex("^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    [GeneratedRegex("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnerPattern();

    [GeneratedRegex("^[a-z]{2,3}(-[A-Z][a-z]{3})?(-[A-Z]{2})?$", RegexOptions.CultureInvariant)]
    private static partial Regex LocalePattern();

    [GeneratedRegex("\\{([A-Za-z_][A-Za-z0-9_]*)\\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();
}
