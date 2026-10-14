using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Qian.Common;

// JSON Schema 动态键模式（patternProperties）的统一解释，以内部源文件编译进需要它的包，见 packages/Common/README.md。
// 模式按 ECMA-262 解释（JSON Schema 2020-12 核心规范 §6.4），Schema 验证器（RegexOptions.ECMAScript）也这样解释：\d \w \s 只认 ASCII。
// 只接受各方言含义相同的写法，逐个记号翻译成 .NET 表达式交给 NonBacktracking；词边界、内联选项、Unicode 属性、命名组、环视、反向引用、代理项等一律拒绝。
internal static class SchemaPattern
{
    // 模式只来自可信契约，进程内条目有限；超过上限整表清空重建，内存有界，已取出的实例不受影响。
    internal const int CacheSize = 128;
    internal static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);
    private const int Universe = char.MaxValue + 1;
    private static readonly ConcurrentDictionary<(string Pattern, bool FoldCase), Regex> Cache = new();
    // 方言判断不同的码元：. 与 $ 涉及的 \n \r U+2028 U+2029、只有 ECMA-262 算作空白的字符（含旧版 Unicode 的 U+180E）、验证器 \w 额外包含的 U+0130。
    private static readonly SearchValues<char> Unstable = SearchValues.Create([
        '\n', '\r', (char)0x00A0, (char)0x0130, (char)0x1680, (char)0x180E, .. Enumerable.Range(0x2000, 11).Select(code => (char)code),
        (char)0x2028, (char)0x2029, (char)0x202F, (char)0x205F, (char)0x3000, (char)0xFEFF]);
    // 每个码元所在 OrdinalIgnoreCase 等价类的代表。配置键（IConfiguration）与 Web 默认反序列化都按它判断键名相等；首次折叠时构建。
    private static readonly Lazy<char[]> Representatives = new(BuildRepresentatives);

    // 键名不含上述码元与代理项（u 标志下按码点处理）时，接受的每种写法在验证器方言与 ECMA-262 下判断相同。
    // 上述码元与代理项在 OrdinalIgnoreCase 下各自成类，折叠后同样成立。
    internal static bool IsStable(string name) => !name.AsSpan().ContainsAny(Unstable) && !name.AsSpan().ContainsAnyInRange((char)0xD800, (char)0xDFFF);

    // NonBacktracking 线性匹配，过大的自动机在构造时即拒绝；超时只兜底超长输入，调用方把构造失败与超时都按 Schema 无效失败关闭。
    // 墙钟只在物化新 DFA 转移和长输入分段时检查：复用实例后，常规长度的热匹配不受调度或 GC 停顿影响；首次 JIT 与建图仍可能被拖长，1 秒上限为此留出余量。
    // 静态 Regex.IsMatch 只靠框架共享的 15 项缓存复用实例，会被其他代码挤出，所以单独缓存。
    // 不支持的写法抛 NotSupportedException，.NET 拒绝的抛 ArgumentException，都直接抛给调用方，不进缓存。
    internal static Regex Compile(string pattern, bool foldCase = false)
    {
        if (Cache.TryGetValue((pattern, foldCase), out var cached)) return cached;
        var regex = new Regex(Translate(pattern, foldCase), RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, MatchTimeout);
        if (Cache.Count >= CacheSize) Cache.Clear();
        return Cache.GetOrAdd((pattern, foldCase), regex);
    }

    internal static int Cached => Cache.Count;

    // 每个单字符位置先算出可匹配的码元集合再输出。foldCase 时把集合扩成 OrdinalIgnoreCase 等价类的并集：
    // 键名能匹配，当且仅当存在一个与它相等（不区分大小写）的键名按原模式能匹配；否定类折叠后通常匹配得更多。
    internal static string Translate(string pattern, bool foldCase = false)
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
                    Emit(output, shorthand == 0 ? Single(value) : Shorthand(shorthand), foldCase);
                    quantifiable = true; break;
                case '[': Emit(output, Class(pattern, ref i), foldCase); quantifiable = true; break;
                case '(':
                    if (i < pattern.Length && pattern[i] == '?')
                    {
                        if (i + 1 >= pattern.Length || pattern[i + 1] != ':') throw Unsupported();
                        i += 2;
                    }
                    output.Append("(?:"); quantifiable = false; break;
                case ')': output.Append(')'); quantifiable = true; break;
                case '|' or '^' or '$': output.Append(c); quantifiable = false; break;
                case '.': Emit(output, Single('\n').Not(), foldCase); quantifiable = true; break;
                case '*' or '+' or '?' or '{':
                    // 量词只能跟在字符、类、. 或分组之后；惰性 ? 之外不能叠加，未组成量词的 { 不当字面量。
                    if (!quantifiable) throw Unsupported();
                    if (c == '{') Repeat(pattern, ref i, output); else output.Append(c);
                    if (i < pattern.Length && pattern[i] == '?') { output.Append('?'); i++; }
                    quantifiable = false; break;
                case '}' or ']': throw Unsupported();
                default: Emit(output, Single(c), foldCase); quantifiable = true; break;
            }
        }
        return output.ToString();
    }

    // [] 与 [^] 在 .NET 与 ECMA-262 含义不同；类内未转义的 [ 可能构成 .NET 的类减法；未转义的 - 只能在类首或类尾。
    // 否定类里再含否定简写的写法拒绝；范围两端只能是单个字符，且不能倒置。
    private static BitArray Class(string pattern, ref int i)
    {
        var negated = i < pattern.Length && pattern[i] == '^';
        if (negated) i++;
        var set = new BitArray(Universe);
        for (var first = true; ; first = false)
        {
            if (i >= pattern.Length || pattern[i] == '[' || (first && pattern[i] == ']')) throw Unsupported();
            if (pattern[i] == ']') { i++; break; }
            if (pattern[i] == '-')
            {
                if (!first && (i + 1 >= pattern.Length || pattern[i + 1] != ']')) throw Unsupported();
                set[pattern[i++]] = true; continue;
            }
            var low = Member(pattern, ref i, out var shorthand);
            if (shorthand != 0)
            {
                if (negated && char.IsUpper(shorthand)) throw Unsupported();
                set.Or(Shorthand(shorthand)); continue;
            }
            var high = low;
            if (i + 1 < pattern.Length && pattern[i] == '-' && pattern[i + 1] != ']')
            {
                if (pattern[++i] is '-' or '[') throw Unsupported();
                high = Member(pattern, ref i, out shorthand);
                if (shorthand != 0 || high < low) throw Unsupported();
            }
            if (char.IsSurrogate(low) || char.IsSurrogate(high)) throw Unsupported();
            for (int code = low; code <= high; code++) set[code] = true;
        }
        return negated ? set.Not() : set;
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

    // \d \w \s 按 ECMA-262 只认 ASCII，大写为补集；ECMA-262 \s 额外的空白都列在 Unstable 里。
    private static BitArray Shorthand(char c)
    {
        var set = new BitArray(Universe);
        switch (char.ToLowerInvariant(c))
        {
            case 'd': Range(set, '0', '9'); break;
            case 'w': Range(set, '0', '9'); Range(set, 'A', 'Z'); Range(set, 'a', 'z'); set['_'] = true; break;
            default: Range(set, '\t', '\r'); set[' '] = true; break;
        }
        return char.IsUpper(c) ? set.Not() : set;
    }

    private static void Range(BitArray set, char low, char high)
    {
        for (int code = low; code <= high; code++) set[code] = true;
    }

    private static BitArray Single(char c)
    {
        if (char.IsSurrogate(c)) throw Unsupported();
        var set = new BitArray(Universe);
        set[c] = true;
        return set;
    }

    // 码元一律写成 \uXXXX，翻译结果里不会残留任何依赖方言的元字符；空集写成全集的补集。
    private static void Emit(StringBuilder output, BitArray set, bool foldCase)
    {
        if (foldCase) set = Fold(set);
        int count = 0, only = 0;
        for (var code = 0; code < Universe && count < 2; code++)
            if (set[code]) { count++; only = code; }
        if (count == 1) { Hex(output, only); return; }
        output.Append(count == 0 ? "[^" : "[");
        if (count == 0) { Hex(output, 0); output.Append('-'); Hex(output, char.MaxValue); }
        for (var code = 0; code < Universe; code++)
        {
            if (!set[code]) continue;
            var start = code;
            while (code + 1 < Universe && set[code + 1]) code++;
            Hex(output, start);
            if (code > start) { output.Append('-'); Hex(output, code); }
        }
        output.Append(']');
    }

    private static void Hex(StringBuilder output, int code) => output.Append('\\').Append('u').Append(code.ToString("X4", CultureInfo.InvariantCulture));

    // 集合扩成等价类的并集：码元 c 在结果里，当且仅当与 c 相等（OrdinalIgnoreCase）的某个码元在原集合里。
    private static BitArray Fold(BitArray set)
    {
        var representatives = Representatives.Value;
        var hit = new BitArray(Universe);
        for (var code = 0; code < Universe; code++)
            if (set[code]) hit[representatives[code]] = true;
        var folded = new BitArray(Universe);
        for (var code = 0; code < Universe; code++) folded[code] = hit[representatives[code]];
        return folded;
    }

    // 直接按 StringComparer.OrdinalIgnoreCase 分组：它与 char.ToUpperInvariant 并不完全一致，键名相等以比较器为准。
    private static char[] BuildRepresentatives()
    {
        var first = new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase);
        var representatives = new char[Universe];
        for (var code = 0; code < Universe; code++)
        {
            var text = new string((char)code, 1);
            if (!first.TryGetValue(text, out var representative)) first[text] = representative = (char)code;
            representatives[code] = representative;
        }
        return representatives;
    }

    private static NotSupportedException Unsupported() => new();
}
