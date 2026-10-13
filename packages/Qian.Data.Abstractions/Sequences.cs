using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Qian.Runtime.Errors;

namespace Qian.Data;

/// <summary>
/// 序号服务：按租户定义的规则原子取号，规则形如 <c>'SO' + yyyyMMdd + seq(6)</c>。
/// 租户取自当前租户上下文；scopeKey 区分租户内的作用域（如应用）。号一经取出即占用，业务事务回滚不归还。
/// </summary>
public interface ISequenceService
{
    /// <summary>定义或修改当前租户的规则；模式变化时修订号加一，各作用域的流水从 1 重新开始，模式不变时原样返回。</summary>
    Task<SequenceRule> DefineAsync(string ruleCode, string pattern, CancellationToken cancellationToken = default);

    /// <summary>取下一个号：同一规则、作用域与日期周期内严格递增、不重复、不跳号。</summary>
    Task<string> NextAsync(string ruleCode, string scopeKey, CancellationToken cancellationToken = default);
}

/// <summary>一条序号规则。</summary>
/// <param name="Code">规则编码。</param>
/// <param name="Pattern">规范化后的模式。</param>
/// <param name="Revision">修订号；模式每变一次加一，流水按修订号分别计数。</param>
public sealed record SequenceRule(string Code, string Pattern, int Revision);

/// <summary>
/// 序号规则模式：用 <c>+</c> 连接的若干段，依次是单引号字面量（如 <c>'SO'</c>）、日期段与恰好一个流水段 <c>seq(n)</c>。
/// 日期段取 yyyy、yyyyMM、yyyyMMdd、yy、yyMM、yyMMdd、MM、dd、MMdd 之一；全部日期段拼成「周期」，周期变化时流水从 1 重新开始。
/// </summary>
public sealed partial class SequencePattern
{
    private static readonly string[] DateFormats = ["yyyy", "yyyyMM", "yyyyMMdd", "yy", "yyMM", "yyMMdd", "MM", "dd", "MMdd"];
    private readonly Part[] _parts;

    private SequencePattern(Part[] parts, int digits)
    {
        _parts = parts;
        Digits = digits;
        MaxValue = (long)Math.Pow(10, digits) - 1;
        Text = string.Join(" + ", parts.Select(part => part.Kind switch
        {
            PartKind.Literal => "'" + part.Value + "'",
            PartKind.Date => part.Value,
            _ => "seq(" + part.Value + ")"
        }));
    }

    /// <summary>规范化后的模式文本，各段之间以 <c> + </c> 分隔。</summary>
    public string Text { get; }

    /// <summary>流水段的位数。</summary>
    public int Digits { get; }

    /// <summary>流水段能表示的最大值。</summary>
    public long MaxValue { get; }

    /// <summary>解析模式；不合法时抛 DATA_SEQUENCE_RULE_INVALID。</summary>
    public static SequencePattern Parse(string pattern)
    {
        if (TryParse(pattern, out var result, out var error)) return result;
        throw new QianErrorException(new QianError(DataErrors.SequenceRuleInvalid, new Dictionary<string, string> { ["reason"] = error }));
    }

    /// <summary>尝试解析模式，失败时给出原因。</summary>
    public static bool TryParse(string? pattern, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out SequencePattern? result, out string error)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(pattern) || pattern.Length > 256) { error = "模式不能为空且不超过 256 个字符。"; return false; }
        var parts = new List<Part>();
        var digits = 0;
        var index = 0;
        while (true)
        {
            while (index < pattern.Length && char.IsWhiteSpace(pattern[index])) index++;
            if (index >= pattern.Length) { error = "模式不能以 + 结尾。"; return false; }
            Part part;
            if (pattern[index] == '\'')
            {
                var end = pattern.IndexOf('\'', index + 1);
                if (end < 0) { error = "字面量缺少结束的单引号。"; return false; }
                var literal = pattern[(index + 1)..end];
                if (literal.Length is 0 or > 32 || literal.Any(char.IsControl)) { error = "字面量须为 1 到 32 个可见字符。"; return false; }
                part = new Part(PartKind.Literal, literal);
                index = end + 1;
            }
            else
            {
                var start = index;
                while (index < pattern.Length && pattern[index] != '+' && !char.IsWhiteSpace(pattern[index])) index++;
                var token = pattern[start..index];
                if (SeqToken().Match(token) is { Success: true } seq)
                {
                    if (digits != 0) { error = "模式只能有一个 seq(n)。"; return false; }
                    digits = int.Parse(seq.Groups[1].Value, CultureInfo.InvariantCulture);
                    if (digits is < 1 or > 18) { error = "seq(n) 的位数须在 1 到 18 之间。"; return false; }
                    part = new Part(PartKind.Sequence, digits.ToString(CultureInfo.InvariantCulture));
                }
                else if (DateFormats.Contains(token, StringComparer.Ordinal)) part = new Part(PartKind.Date, token);
                else { error = $"无法识别的段：{token}。"; return false; }
            }
            parts.Add(part);
            if (parts.Count > 8) { error = "模式最多 8 段。"; return false; }
            while (index < pattern.Length && char.IsWhiteSpace(pattern[index])) index++;
            if (index >= pattern.Length) break;
            if (pattern[index] != '+') { error = "各段之间须用 + 连接。"; return false; }
            index++;
        }
        if (digits == 0) { error = "模式必须包含一个 seq(n)。"; return false; }
        var length = parts.Sum(part => part.Kind switch { PartKind.Literal => part.Value.Length, PartKind.Date => part.Value.Length, _ => digits });
        if (length > 64) { error = "生成的序号不能超过 64 个字符。"; return false; }
        result = new SequencePattern([.. parts], digits);
        error = "";
        return true;
    }

    /// <summary>某一时刻所在的周期：全部日期段的取值依次拼接，没有日期段时为空串（流水永不重置）。</summary>
    public string Period(DateTime local)
    {
        var builder = new StringBuilder();
        foreach (var part in _parts)
        {
            if (part.Kind != PartKind.Date) continue;
            if (builder.Length > 0) builder.Append('|');
            builder.Append(local.ToString(part.Value, CultureInfo.InvariantCulture));
        }
        return builder.ToString();
    }

    /// <summary>按模式输出序号；流水左侧补零到 <see cref="Digits"/> 位。</summary>
    public string Format(DateTime local, long value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxValue);
        var builder = new StringBuilder();
        foreach (var part in _parts)
        {
            builder.Append(part.Kind switch
            {
                PartKind.Literal => part.Value,
                PartKind.Date => local.ToString(part.Value, CultureInfo.InvariantCulture),
                _ => value.ToString("D" + Digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
            });
        }
        return builder.ToString();
    }

    /// <inheritdoc />
    public override string ToString() => Text;

    [GeneratedRegex(@"^seq\((\d{1,2})\)$", RegexOptions.CultureInvariant)]
    private static partial Regex SeqToken();

    private enum PartKind { Literal, Date, Sequence }

    private sealed record Part(PartKind Kind, string Value);
}
