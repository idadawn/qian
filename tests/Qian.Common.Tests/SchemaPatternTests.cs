using System.Text.RegularExpressions;

namespace Qian.Common.Tests;

public sealed class SchemaPatternTests
{
    // V8（Node 26）对全部 BMP 码元测得的 ECMA-262 \s，带不带 u 标志相同；同一测量下 \w 恰为 [A-Za-z0-9_]、\d 恰为 [0-9]，. 不匹配 \n \r U+2028 U+2029。
    private static readonly HashSet<char> EcmaSpace = [.. "\t\n\v\f\r ", (char)0x00A0, (char)0x1680, .. Enumerable.Range(0x2000, 11).Select(code => (char)code),
        (char)0x2028, (char)0x2029, (char)0x202F, (char)0x205F, (char)0x3000, (char)0xFEFF];
    // 差分语料：模式都在接受的子集内，键名都不含方言判断不同的码元；Expected 每行对应一个模式、每列对应一个键名，由 V8 以 u 与非 u 两种模式独立计算且结果相同。
    private static readonly string[] Patterns = [
        @"^secret_", @"_token$", @"^x-[a-z0-9]+$", @"^(api|app)[_-]?key$", @"^\d+$", @"^\D+$", @"^\w+$", @"^\W+$",
        @"^\s*$", @"\S", @"^[\w.-]+$", @"^[^\s]+$", @"^a.c$", @"^.{2,3}$", @"^(?:ab)+?$", @"^\$\{[a-z]+\}$",
        @"^[\]\[]$", @"^\x41\u4e2d$", @"^[-a]$", @"^[a-]$", @"^[^-]$", @"^\t?\.\*$", @"a|b|", @"",
        @"^$", @"^é+$", @"^客户[0-9]{1,3}$", @"^[\d\s]+$", @"x{3}", @"^x{2,}$", @"^(a|b)*c$", @"^[^a-z]*$",
        @"^[\/]$", @"^\w*?\d$", @"^[A-Z][a-z]*(?:_[A-Z][a-z]*)*$", @"^[^\d]?[\s\S]$", @"^[a\D]+$",
    ];
    private static readonly string[] Keys = [
        "", "a", "c", "x", "xx", "xxx", "ab", "abab", "abc", "ac", "bc", "secret_x", "secret", "my_token",
        "token_x", "x-abc1", "x-", "X-abc", "app_key", "apikey", "api-key", "123", "١٢٣", "1a", "é", "éé", "eé", "客户12",
        "客户1234", "客户", "a c", " ", "\t", "\t.*", ".*", "${abc}", "${}", "]", "[", "A中", "-", "^",
        "a.c", "a-c", "_", "a_b", "Abc_Def", "Abc_", "/", "ß", "ÄÖ", "\v", "\f", "١", "é1",
    ];
    private static readonly string[] Expected = [
        "0000000000010000000000000000000000000000000000000000000",
        "0000000000000100000000000000000000000000000000000000000",
        "0000000000000001000000000000000000000000000000000000000",
        "0000000000000000001110000000000000000000000000000000000",
        "0000000000000000000001000000000000000000000000000000000",
        "0111111111111110111110101110011111111111111111111111110",
        "0111111111111110001101010000000000000000000011110000000",
        "0000000000000000000000101100010111101110110000001111110",
        "1000000000000000000000000000000110000000000000000001100",
        "0111111111111111111111111111111001111111111111111110011",
        "0111111111111111111111010000000000000000101111110000000",
        "0111111111111111111111111111110000111111111111111110011",
        "0000000010000000000000000000001000000000001100000000000",
        "0000111011100000100001110110011001101001001101000010001",
        "0000001100000000000000000000000000000000000000000000000",
        "0000000000000000000000000000000000010000000000000000000",
        "0000000000000000000000000000000000000110000000000000000",
        "0000000000000000000000000000000000000001000000000000000",
        "0100000000000000000000000000000000000000100000000000000",
        "0100000000000000000000000000000000000000100000000000000",
        "0111000000000000000000001000000110000110010010001101110",
        "0000000000000000000000000000000001100000000000000000000",
        "1111111111111111111111111111111111111111111111111111111",
        "1111111111111111111111111111111111111111111111111111111",
        "1000000000000000000000000000000000000000000000000000000",
        "0000000000000000000000001100000000000000000000000000000",
        "0000000000000000000000000001000000000000000000000000000",
        "0000000000000000000001000000000110000000000000000001100",
        "0000010000000000000000000000000000000000000000000000000",
        "0000110000000000000000000000000000000000000000000000000",
        "0010000011100000000000000000000000000000000000000000000",
        "1000000000000000000001101101110111101111110010001111111",
        "0000000000000000000000000000000000000000000000001000000",
        "0000000000000000000001000000000000000000000000000000000",
        "0000000000000000000000000000000000000000000000100000000",
        "0111101001100000100000001110010110100111110010001111111",
        "0111111111111110111110101110011111111111111111111111110",
    ];

    private static Regex Compile(string pattern) => new(SchemaPattern.Translate(pattern), RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static Regex Validator(string pattern) => new(pattern, RegexOptions.ECMAScript);
    private static bool Ecma(string token, char c) => token switch
    {
        @"\w" => char.IsAsciiLetterOrDigit(c) || c == '_', @"\W" => !(char.IsAsciiLetterOrDigit(c) || c == '_'),
        @"\d" => char.IsAsciiDigit(c), @"\D" => !char.IsAsciiDigit(c),
        @"\s" => EcmaSpace.Contains(c), @"\S" => !EcmaSpace.Contains(c),
        _ => c is not ('\n' or '\r' or (char)0x2028 or (char)0x2029),
    };

    [Fact]
    public void 方言判断不同的码元都不稳定_其余码元上翻译后的字符类与验证器方言和ECMA262逐个一致()
    {
        foreach (var code in new[] { 0x0A, 0x0D, 0x00A0, 0x0130, 0x1680, 0x180E, 0x2000, 0x200A, 0x2028, 0x2029, 0x202F, 0x205F, 0x3000, 0xFEFF, 0xD83D, 0xDE00 })
            Assert.False(SchemaPattern.IsStable("key" + (char)code));
        foreach (var token in new[] { @"\w", @"\W", @"\d", @"\D", @"\s", @"\S", "." })
        {
            var ours = Compile($"^{token}$"); var validator = Validator($"^{token}$");
            for (var code = 0; code <= char.MaxValue; code++)
            {
                var text = ((char)code).ToString();
                if (!SchemaPattern.IsStable(text)) continue;
                var expected = Ecma(token, (char)code);
                if (ours.IsMatch(text) != expected || validator.IsMatch(text) != expected) Assert.Fail($"{token} 在 U+{code:X4} 上方言不一致");
            }
        }
    }

    [Fact]
    public void 方言一致的键名上_翻译结果与ECMA262固定向量和验证器方言逐例一致()
    {
        Assert.All(Keys, key => Assert.True(SchemaPattern.IsStable(key)));
        // 另穷举长度 0–3 的短键名，逐例对照验证器方言。
        const string alphabet = "abcx_1-. é١客";
        IEnumerable<string> Strings(int length) => length == 0 ? [""] : Strings(length - 1).SelectMany(prefix => alphabet.Select(c => prefix + c));
        var exhaustive = Enumerable.Range(0, 4).SelectMany(Strings).ToArray();
        for (var row = 0; row < Patterns.Length; row++)
        {
            var ours = Compile(Patterns[row]); var validator = Validator(Patterns[row]);
            Assert.Equal((Patterns[row], Expected[row]), (Patterns[row], string.Concat(Keys.Select(key => ours.IsMatch(key) ? '1' : '0'))));
            Assert.Equal((Patterns[row], Expected[row]), (Patterns[row], string.Concat(Keys.Select(key => validator.IsMatch(key) ? '1' : '0'))));
            foreach (var key in exhaustive)
                if (ours.IsMatch(key) != validator.IsMatch(key)) Assert.Fail($"{Patterns[row]} 与验证器方言在 [{key}] 上不一致");
        }
    }

    [Fact]
    public void 各方言含义不同或无法线性执行的写法在翻译时拒绝()
    {
        string[] rejected = [
            @"\bx", @"\Bx", "(?i)a", "(?<n>a)", "(?'n'a)", "(?=a)", "(?!a)", "(?<=a)b", "(?<!a)b", "(?>a)", "(?#c)", "(?(a)b)",
            @"\p{L}", @"\P{L}", @"\A", @"\Z", @"\z", @"\G", @"\e", @"\cA", @"\0", @"\1", @"\k<n>", @"\-", @"\ ", @"\#", @"\x4", @"\", "\\u{41}",
            "[a-z-[aeiou]]", "[]a]", "[^]", @"[^\D]", @"[^\W_]", @"[^\S]", @"[\d-z]", @"[a-\d]", "[a-b-c]", "[a--]", "[z-a]", "[abc", @"[\b]",
            "a{", "a{1", "a{,2}", "a{x}", "}", "]", "*a", "^*", "$+", "(*a)", "a|*", "a**", "a*??", "a{2}{3}",
            "😀", "[😀]", @"\" + "uD83D", @"[\" + "uDE00]"];
        foreach (var pattern in rejected)
        {
            Assert.Throws<NotSupportedException>(() => SchemaPattern.Translate(pattern));
            Assert.Throws<NotSupportedException>(() => SchemaPattern.Translate(pattern, foldCase: true));
        }
    }

    // 按 StringComparer.OrdinalIgnoreCase 把全部 BMP 码元分成等价类，即配置键判断相等的口径。
    private static readonly Lazy<Dictionary<char, char[]>> Classes = new(() =>
    {
        var groups = new Dictionary<string, List<char>>(StringComparer.OrdinalIgnoreCase);
        for (var code = 0; code <= char.MaxValue; code++)
        {
            var text = new string((char)code, 1);
            if (!groups.TryGetValue(text, out var list)) groups[text] = list = [];
            list.Add((char)code);
        }
        return groups.Values.SelectMany(list => list.Select(member => (member, list.ToArray()))).ToDictionary(pair => pair.member, pair => pair.Item2);
    });
    // 折叠专用语料：大小写字面量、否定类、Latin-1 之外才有大写的 U+00FF、三元等价类 U+00B5 与 U+01C5。
    private static readonly string[] FoldPatterns = [
        "^legacy_", "^X-", "^[A-Z]+$", "^[^a-z]+$", "^[^A-Za-z]+$", "^(?:a|A)x$", "^[a-z][a-z0-9-]*$",
        "^" + (char)0x00C9 + "+$", "^[" + (char)0x00E0 + "-" + (char)0x00FF + "]+$", "^" + (char)0x00B5 + "x?$", "^[" + (char)0x01C5 + "]$",
    ];

    private static Regex Folded(string pattern) => new(SchemaPattern.Translate(pattern, foldCase: true), RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    [Fact]
    public void 折叠前提_方言判断不同的码元与代理项各自成类_稳定码元的等价类里只有稳定码元()
    {
        for (var code = 0; code <= char.MaxValue; code++)
        {
            var members = Classes.Value[(char)code];
            var stable = SchemaPattern.IsStable(new string((char)code, 1));
            if (stable ? members.Any(member => !SchemaPattern.IsStable(new string(member, 1))) : members.Length != 1)
                Assert.Fail($"U+{code:X4} 的等价类破坏了折叠前提");
        }
    }

    [Fact]
    public void 折叠后的单字符写法_逐码元等于存在相等码元按验证器方言匹配()
    {
        string[] tokens = [@"\w", @"\W", @"\d", @"\D", @"\s", @"\S", ".", "[a-z]", "[^a-z]", "[^A-Z0-9_]", "k", "_",
            new string((char)0x00E9, 1), new string((char)0x00B5, 1), new string((char)0x01C5, 1), "[" + (char)0x03C2 + "-" + (char)0x03C9 + "]"];
        foreach (var token in tokens)
        {
            var folded = Folded($"^{token}$"); var validator = Validator($"^{token}$");
            var direct = new bool[char.MaxValue + 1];
            for (var code = 0; code <= char.MaxValue; code++) direct[code] = validator.IsMatch(new string((char)code, 1));
            for (var code = 0; code <= char.MaxValue; code++)
            {
                var text = new string((char)code, 1);
                if (!SchemaPattern.IsStable(text)) continue;
                if (folded.IsMatch(text) != Classes.Value[(char)code].Any(member => direct[member])) Assert.Fail($"{token} 折叠后在 U+{code:X4} 上与等价类判定不一致");
            }
        }
    }

    [Fact]
    public void 折叠后的模式_在短键穷举上等于存在相等键名按验证器方言匹配()
    {
        // 字母表含大小写成对的 ASCII 与非 ASCII 字母、三元等价类与没有大小写的码元；变体逐位取等价类成员。
        var alphabet = "aAx_1-" + (char)0x00E9 + (char)0x00C9 + (char)0x00B5;
        IEnumerable<string> Strings(int length) => length == 0 ? [""] : Strings(length - 1).SelectMany(prefix => alphabet.Select(c => prefix + c));
        IEnumerable<string> Variants(string key) => key.Length == 0 ? [""] : Variants(key[..^1]).SelectMany(prefix => Classes.Value[key[^1]].Select(c => prefix + c));
        var keys = Enumerable.Range(0, 4).SelectMany(Strings).ToArray();
        foreach (var pattern in Patterns.Concat(FoldPatterns))
        {
            var folded = Folded(pattern); var validator = Validator(pattern);
            foreach (var key in keys)
                if (folded.IsMatch(key) != Variants(key).Any(validator.IsMatch)) Assert.Fail($"{pattern} 折叠后在 [{key}] 上与相等键名的判定不一致");
        }
        // 不折叠时保持区分大小写，与审计面对的 JSON 一致。
        Assert.DoesNotMatch(Compile("^legacy_"), "Legacy_x");
        Assert.Matches(Folded("^legacy_"), "LEGACY_X");
    }

    [Fact]
    public void 编译实例按模式与是否折叠分别复用_引擎与超时固定_失败不进缓存_缓存有上限()
    {
        var plain = SchemaPattern.Compile("^legacy_");
        var folded = SchemaPattern.Compile("^legacy_", foldCase: true);
        Assert.Same(plain, SchemaPattern.Compile("^legacy_"));
        Assert.Same(folded, SchemaPattern.Compile("^legacy_", foldCase: true));
        Assert.NotSame(plain, folded);
        foreach (var regex in new[] { plain, folded })
        {
            Assert.Equal(RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, regex.Options);
            Assert.Equal(TimeSpan.FromSeconds(1), regex.MatchTimeout);
        }
        // 不支持的写法、.NET 拒绝的量词与超限自动机，第二次仍抛出。
        foreach (var pattern in new[] { "(?=x)", "a{2,1}", "a{20000}" })
            for (var attempt = 0; attempt < 2; attempt++)
                Assert.ThrowsAny<Exception>(() => SchemaPattern.Compile(pattern, foldCase: attempt == 1));
        for (var index = 0; index <= SchemaPattern.CacheSize; index++) _ = SchemaPattern.Compile($"^bounded_{index}$", index % 2 == 0);
        Assert.InRange(SchemaPattern.Cached, 1, SchemaPattern.CacheSize);
    }
}
