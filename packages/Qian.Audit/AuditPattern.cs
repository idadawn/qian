using System.Buffers;
using System.Globalization;
using System.Text;

namespace Qian.Audit;

// 动态键模式按 ECMA-262 解释（JSON Schema 2020-12 核心规范 §6.4），Schema 验证器（RegexOptions.ECMAScript）也这样解释：\d \w \s 只认 ASCII。
// 只接受各方言含义相同的写法，逐个记号翻译成 .NET 表达式交给 NonBacktracking；词边界、内联选项、Unicode 属性、命名组、环视、反向引用、代理项等一律拒绝。
internal static class AuditPattern
{
    private const string Digit = "0-9", Word = "A-Za-z0-9_", Space = "\\t\\n\\v\\f\\r ";
    // 方言判断不同的码元：. 与 $ 涉及的 \n \r U+2028 U+2029、只有 ECMA-262 算作空白的字符（含旧版 Unicode 的 U+180E）、验证器 \w 额外包含的 U+0130。
    private static readonly SearchValues<char> Unstable = SearchValues.Create([
        '\n', '\r', (char)0x00A0, (char)0x0130, (char)0x1680, (char)0x180E, .. Enumerable.Range(0x2000, 11).Select(code => (char)code),
        (char)0x2028, (char)0x2029, (char)0x202F, (char)0x205F, (char)0x3000, (char)0xFEFF]);

    // 键名不含上述码元与代理项（u 标志下按码点处理）时，接受的每种写法在验证器方言与 ECMA-262 下判断相同。
    internal static bool IsStable(string name) => !name.AsSpan().ContainsAny(Unstable) && !name.AsSpan().ContainsAnyInRange((char)0xD800, (char)0xDFFF);

    internal static string Translate(string pattern)
    {
        var output = new StringBuilder(pattern.Length * 6);
        var quantifiable = false;
        for (var i = 0; i < pattern.Length;)
        {
            var c = pattern[i++];
            switch (c)
            {
                case '\\':
                    var value = Escape(pattern, ref i, false, out var shorthand);
                    if (shorthand == 0) Literal(output, value);
                    else output.Append(char.IsUpper(shorthand) ? "[^" : "[").Append(Shorthand(shorthand)).Append(']');
                    quantifiable = true; break;
                case '[': Class(pattern, ref i, output); quantifiable = true; break;
                case '(':
                    if (i < pattern.Length && pattern[i] == '?')
                    {
                        if (i + 1 >= pattern.Length || pattern[i + 1] != ':') throw Unsupported();
                        i += 2;
                    }
                    output.Append("(?:"); quantifiable = false; break;
                case ')': output.Append(')'); quantifiable = true; break;
                case '|' or '^' or '$': output.Append(c); quantifiable = false; break;
                case '.': output.Append("[^\\n]"); quantifiable = true; break;
                case '*' or '+' or '?' or '{':
                    // 量词只能跟在字符、类、. 或分组之后；惰性 ? 之外不能叠加，未组成量词的 { 不当字面量。
                    if (!quantifiable) throw Unsupported();
                    if (c == '{') Repeat(pattern, ref i, output); else output.Append(c);
                    if (i < pattern.Length && pattern[i] == '?') { output.Append('?'); i++; }
                    quantifiable = false; break;
                case '}' or ']': throw Unsupported();
                default: Literal(output, c); quantifiable = true; break;
            }
        }
        return output.ToString();
    }

    // [] 与 [^] 在 .NET 与 ECMA-262 含义不同；类内未转义的 [ 可能构成 .NET 的类减法；未转义的 - 只能在类首或类尾。
    // 类内的否定简写改写成分支：[x\S] 即 (?:[x]|[^空白])；否定类里再含否定简写的写法拒绝。
    private static void Class(string pattern, ref int i, StringBuilder output)
    {
        var negated = i < pattern.Length && pattern[i] == '^';
        if (negated) i++;
        var members = new StringBuilder();
        var complements = new List<string>();
        for (var first = true; ; first = false)
        {
            if (i >= pattern.Length || pattern[i] == '[' || (first && pattern[i] == ']')) throw Unsupported();
            if (pattern[i] == ']') { i++; break; }
            if (pattern[i] == '-')
            {
                if (!first && (i + 1 >= pattern.Length || pattern[i + 1] != ']')) throw Unsupported();
                Literal(members, pattern[i++]); continue;
            }
            var low = Member(pattern, ref i, out var shorthand);
            if (shorthand != 0)
            {
                if (!char.IsUpper(shorthand)) members.Append(Shorthand(shorthand));
                else if (negated) throw Unsupported();
                else complements.Add(Shorthand(shorthand));
                continue;
            }
            Literal(members, low);
            if (i + 1 < pattern.Length && pattern[i] == '-' && pattern[i + 1] != ']')
            {
                if (pattern[++i] is '-' or '[') throw Unsupported();
                var high = Member(pattern, ref i, out shorthand);
                if (shorthand != 0) throw Unsupported();
                members.Append('-'); Literal(members, high);
            }
        }
        if (complements.Count == 0) { output.Append(negated ? "[^" : "[").Append(members).Append(']'); return; }
        output.Append("(?:");
        if (members.Length > 0) output.Append('[').Append(members).Append("]|");
        output.AppendJoin('|', complements.Select(set => $"[^{set}]")).Append(')');
    }

    private static char Member(string pattern, ref int i, out char shorthand)
    {
        shorthand = '\0';
        var c = pattern[i++];
        return c == '\\' ? Escape(pattern, ref i, true, out shorthand) : c;
    }

    // 只认 \d \D \w \W \s \S、\t \n \r \f \v、\xHH、\uHHHH 与语法字符的转义（类内另可转义 -）；\b、\A、\p、\k、\0、\1 等在各方言含义不同，拒绝。
    private static char Escape(string pattern, ref int i, bool inClass, out char shorthand)
    {
        shorthand = '\0';
        if (i >= pattern.Length) throw Unsupported();
        var c = pattern[i++];
        switch (c)
        {
            case 'd' or 'D' or 'w' or 'W' or 's' or 'S': shorthand = c; return c;
            case 't': return '\t';
            case 'n': return '\n';
            case 'r': return '\r';
            case 'f': return '\f';
            case 'v': return '\v';
            case 'x' or 'u':
                var digits = c == 'x' ? 2 : 4;
                if (i + digits > pattern.Length || !int.TryParse(pattern.AsSpan(i, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code))
                    throw Unsupported();
                i += digits; return (char)code;
            case '-' when inClass: return c;
            default: return "^$\\.*+?()[]{}|/".Contains(c) ? c : throw Unsupported();
        }
    }

    private static void Repeat(string pattern, ref int i, StringBuilder output)
    {
        var end = i;
        while (end < pattern.Length && char.IsAsciiDigit(pattern[end])) end++;
        if (end == i) throw Unsupported();
        if (end < pattern.Length && pattern[end] == ',') { end++; while (end < pattern.Length && char.IsAsciiDigit(pattern[end])) end++; }
        if (end >= pattern.Length || pattern[end] != '}') throw Unsupported();
        output.Append('{').Append(pattern, i, end + 1 - i);
        i = end + 1;
    }

    private static string Shorthand(char c) => char.ToLowerInvariant(c) switch { 'd' => Digit, 'w' => Word, _ => Space };

    // 字面量一律写成 \uXXXX，翻译结果里不会残留任何依赖方言的元字符。
    private static void Literal(StringBuilder output, char c)
    {
        if (char.IsSurrogate(c)) throw Unsupported();
        output.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
    }

    private static NotSupportedException Unsupported() => new();
}
