using System.Globalization;
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

    // 值层语料：Schema 验证器也用翻译结果判断字符串值，值里可能有方言判断不同的码元、代理对与残缺代理项。
    // 只对不折叠的翻译结果计算；CLI 测试另用 V8 不带标志的 RegExp 逐例对照，确认翻译结果按 ECMA-262 逐码元判断。
    private static readonly string[] Values = [
        "a\n", "\n", "a\r\nb", "\r", "a" + (char)0x2028 + "b", new string((char)0x2029, 1), "abc\n", "secret_\n", "x-abc1\n", "my_token\n",
        new string((char)0x00A0, 1), new string((char)0x3000, 1), new string((char)0xFEFF, 1), new string((char)0x180E, 1), new string((char)0x0085, 1), " \t",
        new string((char)0x0130, 1), "a" + (char)0x0130, new string((char)0x0131, 1), char.ConvertFromUtf32(0x1F600), "a" + char.ConvertFromUtf32(0x1F600) + "c",
        new string((char)0xD83D, 1), (char)0xDE00 + "x", "12" + (char)0x0661, "客户" + char.ConvertFromUtf32(0x20000),
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
    public void 翻译后的字符类在全部码元上按ECMA262判断_验证器方言只在稳定码元上与之一致()
    {
        foreach (var code in new[] { 0x0A, 0x0D, 0x00A0, 0x0130, 0x1680, 0x180E, 0x2000, 0x200A, 0x2028, 0x2029, 0x202F, 0x205F, 0x3000, 0xFEFF, 0xD83D, 0xDE00 })
            Assert.False(SchemaPattern.IsStable("key" + (char)code));
        foreach (var token in new[] { @"\w", @"\W", @"\d", @"\D", @"\s", @"\S", "." })
        {
            var ours = Compile($"^{token}$"); var validator = Validator($"^{token}$");
            for (var code = 0; code <= char.MaxValue; code++)
            {
                var text = ((char)code).ToString();
                var expected = Ecma(token, (char)code);
                if (ours.IsMatch(text) != expected) Assert.Fail($"{token} 在 U+{code:X4} 上与 ECMA-262 不一致");
                if (SchemaPattern.IsStable(text) && validator.IsMatch(text) != expected) Assert.Fail($"{token} 在稳定码元 U+{code:X4} 上方言不一致");
            }
        }
        // $ 只在末尾成立；.NET 自己的 $ 还匹配末尾换行之前，换行因此不稳定。
        Assert.Matches(Compile("a$"), "xa");
        Assert.DoesNotMatch(Compile("a$"), "a\n");
        Assert.Matches(Validator("a$"), "a\n");
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

    // 各方言含义不同、无法线性执行、分组未配对、量词倒置或超出规模的写法。
    private static readonly string[] Rejected = [
        @"\bx", @"\Bx", "(?i)a", "(?<n>a)", "(?'n'a)", "(?=a)", "(?!a)", "(?<=a)b", "(?<!a)b", "(?>a)", "(?#c)", "(?(a)b)",
        @"\p{L}", @"\P{L}", @"\A", @"\Z", @"\z", @"\G", @"\e", @"\cA", @"\0", @"\1", @"\k<n>", @"\-", @"\ ", @"\#", @"\x4", @"\", "\\u{41}",
        "[a-z-[aeiou]]", "[]a]", "[^]", @"[^\D]", @"[^\W_]", @"[^\S]", @"[\d-z]", @"[a-\d]", "[a-b-c]", "[a--]", "[z-a]", "[abc", @"[\b]",
        "a{", "a{1", "a{,2}", "a{x}", "}", "]", "*a", "^*", "$+", "(*a)", "a|*", "a**", "a*??", "a{2}{3}",
        "(a", "a)", "(?:a))", ")(", "a{2,1}", "a{1000,999}", "a{1001}", "a{0,1001}", "a{1001,}", "(?:){1001}", "a{00000000001001}",
        "^a{999}$", "^a{998,}$", "(?:a{100}){10}b", "(?:(?:^|a){10}){50}a",
        "😀", "[😀]", @"\" + "uD83D", @"[\" + "uDE00]"];
    // 展开后恰为 MaxSize 的写法：锚点密集、分组嵌套、交替与开区间量词，覆盖 NonBacktracking 估算最高的形状（含锚点时约为规模的 5 倍）。
    private static readonly string[] AtLimit = [
        "^a{998}$", "^(?:a|b){499}$", "(?:^a$){333}a", "(?:(?:^|a){10}){50}", "^(?:a*b){499}$", "^a{997,}$", "^(?:a{1,2}?){499}$",
        "^(?:[^a]|.){499}$", "(?:){1000}a{1000}", string.Concat(Enumerable.Repeat("^a$", 333)) + "a",
        "^(?:" + string.Join("|", Enumerable.Range(0, 499).Select(i => (char)(0x4E00 + i) + "a")) + ")$"];

    [Fact]
    public void 各方言含义不同_无法线性执行_未配对_倒置或超出规模的写法在翻译时拒绝()
    {
        foreach (var pattern in Rejected)
        {
            Assert.Throws<NotSupportedException>(() => SchemaPattern.Translate(pattern));
            Assert.Throws<NotSupportedException>(() => SchemaPattern.Translate(pattern, foldCase: true));
        }
    }

    [Fact]
    public void 规模恰在上限的写法都能构造_再多一个字符即在翻译时拒绝()
    {
        foreach (var pattern in AtLimit)
        {
            // 接受的模式必须能被 .NET 构造，否则 CLI 与 .NET 的接受集合又会分开。
            _ = Compile(pattern);
            _ = Folded(pattern);
            Assert.Throws<NotSupportedException>(() => SchemaPattern.Translate(pattern + "a"));
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
        // 不支持的写法、倒置的量词与超出规模的写法，第二次仍抛出。
        foreach (var pattern in new[] { "(?=x)", "a{2,1}", "a{20000}" })
            for (var attempt = 0; attempt < 2; attempt++)
                Assert.Throws<NotSupportedException>(() => SchemaPattern.Compile(pattern, foldCase: attempt == 1));
        for (var index = 0; index <= SchemaPattern.CacheSize; index++) _ = SchemaPattern.Compile($"^bounded_{index}$", index % 2 == 0);
        Assert.InRange(SchemaPattern.Cached, 1, SchemaPattern.CacheSize);
    }

    // tck/profiles/key-vectors.json 与 packages/qian-cli/src/ordinal-case.ts 由本实现生成，CLI 的 TS 移植（schema-pattern.ts）逐条比对。
    // 改了 SchemaPattern、升级 .NET 或宿主 ICU 新增大小写对后，以 QIAN_UPDATE_VECTORS=1 运行本测试重新生成，再同步 TS 移植直到 CLI 测试通过。
    [Fact]
    public void 跨语言向量与等价类表由本实现生成_与仓库文件逐字一致()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        foreach (var (relative, content) in new[] { ("tck/profiles/key-vectors.json", Vectors()), ("packages/qian-cli/src/ordinal-case.ts", CaseTable()) })
        {
            var path = Path.Combine(root.FullName, relative);
            if (Environment.GetEnvironmentVariable("QIAN_UPDATE_VECTORS") == "1") File.WriteAllText(path, content);
            var actual = File.Exists(path) ? File.ReadAllText(path) : "";
            Assert.True(actual == content, $"{relative} 与本实现不一致：以 QIAN_UPDATE_VECTORS=1 运行本测试重新生成，再同步 CLI 的 schema-pattern.ts。" + Difference(actual, content));
        }
    }

    // 按行列出仓库文件与本实现各自多出的内容（各取前 10 行，每行截到 200 个字符），宿主环境不同时便于定位。
    private static string Difference(string actual, string expected)
    {
        static string Lines(IEnumerable<string> lines) => string.Join("\n  ", lines.Take(10).Select(line => line.Length > 200 ? line[..200] + "…" : line));
        var (left, right) = (actual.Split('\n'), expected.Split('\n'));
        return "\n仓库文件多出：\n  " + Lines(left.Except(right)) + "\n本实现多出：\n  " + Lines(right.Except(left));
    }

    // 翻译结果与在 Keys 上的匹配（不折叠与折叠）、在 Values 上的匹配（不折叠）、全部 BMP 码元的稳定性、键名相等，都按本实现与 .NET 比较器给出。
    private static string Vectors()
    {
        var unstable = new List<string>();
        for (var code = 0; code <= char.MaxValue; code++)
        {
            if (SchemaPattern.IsStable(new string((char)code, 1))) continue;
            var start = code;
            while (code < char.MaxValue && !SchemaPattern.IsStable(new string((char)(code + 1), 1))) code++;
            unstable.Add($"[{start}, {code}]");
        }
        // 恰在上限的写法只收短的，各配一个多一个字符的反例；长写法的翻译结果冗长且没有新的记号。
        var limit = AtLimit.Where(pattern => pattern.Length <= 64).ToArray();
        var patterns = Patterns.Concat(FoldPatterns).Concat(limit).Concat(limit.Select(pattern => pattern + "a")).Concat(Rejected).Select(pattern =>
        {
            string plain;
            try { plain = SchemaPattern.Translate(pattern); }
            catch (NotSupportedException)
            {
                Assert.Throws<NotSupportedException>(() => SchemaPattern.Translate(pattern, foldCase: true));
                return $"{{\"pattern\": {Quote(pattern)}, \"rejected\": true}}";
            }
            var folded = SchemaPattern.Translate(pattern, foldCase: true);
            return $"{{\"pattern\": {Quote(pattern)}, \"plain\": {Quote(plain)}, \"matches\": \"{Bits(plain, Keys)}\", \"valueMatches\": \"{Bits(plain, Values)}\", " +
                $"\"folded\": {Quote(folded)}, \"foldedMatches\": \"{Bits(folded, Keys)}\"}}";
        });
        var equality = EqualityPairs().Select(pair =>
            $"{{\"left\": {Quote(pair.Left)}, \"right\": {Quote(pair.Right)}, \"equal\": {(string.Equals(pair.Left, pair.Right, StringComparison.OrdinalIgnoreCase) ? "true" : "false")}}}");
        return "{\n" +
            $"  \"maxSize\": {SchemaPattern.MaxSize},\n" +
            $"  \"unstable\": [{string.Join(", ", unstable)}],\n" +
            $"  \"keys\": [{string.Join(", ", Keys.Select(Quote))}],\n" +
            $"  \"values\": [{string.Join(", ", Values.Select(Quote))}],\n" +
            $"  \"patterns\": [\n    {string.Join(",\n    ", patterns)}\n  ],\n" +
            $"  \"equality\": [\n    {string.Join(",\n    ", equality)}\n  ]\n}}\n";
    }

    private static string Bits(string translated, string[] texts)
    {
        var regex = new Regex(translated, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
        return string.Concat(texts.Select(text => regex.IsMatch(text) ? '1' : '0'));
    }

    // 只原样输出可打印 ASCII，其余码元一律写成 JSON 转义，残缺的代理项也能原样往返。
    private static string Quote(string text) => "\"" + string.Concat(text.Select(c => c switch
    {
        '"' => "\\\"", '\\' => "\\\\", >= ' ' and <= '~' => c.ToString(),
        _ => "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture),
    })) + "\"";

    // 随机组合用的码点：与 ASCII 字母相关的特殊码元、三元等价类、增补平面的一对大小写与残缺代理项。
    private static readonly int[] TokenCodes = [0xDF, 0x17F, 0x131, 0x130, 0x212A, 0xB5, 0x39C, 0x3BC, 0x1C4, 0x1C5, 0x1C6, 0x3C2, 0x3C3, 0x3A3, 0xE9, 0xC9, 0x10400, 0x10428, 0xD801, 0xDC00, 0xDC28];

    // 键名相等的向量：Unicode 大小写转换与 OrdinalIgnoreCase 结论不同的码元、三元等价类、增补平面与残缺代理项，另加固定种子的随机组合。
    private static IEnumerable<(string Left, string Right)> EqualityPairs()
    {
        static string S(params int[] codes) => string.Concat(codes.Select(code => code > char.MaxValue ? char.ConvertFromUtf32(code) : new string((char)code, 1)));
        (string, string)[] chosen = [
            ("Secret", "secret"), ("LEGACY_X", "legacy_x"), ("a", "aa"), ("", ""), ("a", ""),
            (S(0xDF), "ss"), (S(0xDF), "SS"), (S(0x1E9E), S(0xDF)), (S(0x17F), "s"), (S(0x17F), "S"), (S(0x131), "I"), (S(0x131), "i"),
            (S(0x130), "i"), (S(0x130), "I"), (S(0x212A), "k"), (S(0x212A), "K"), (S(0x212B), S(0xE5)), (S(0x2126), S(0x3C9)),
            (S(0xB5), S(0x39C)), (S(0xB5), S(0x3BC)), (S(0x1C5), S(0x1C4)), (S(0x1C5), S(0x1C6)), (S(0x3C3), S(0x3C2)), (S(0x3C2), S(0x3A3)),
            (S(0xE9), S(0xC9)), (S(0xFF), S(0x178)), (S(0x10400), S(0x10428)), ("x" + S(0x10400), "X" + S(0x10428)), (S(0x10400), S(0x10401)),
            (S(0x1E900), S(0x1E922)), (S(0xD801, 'a'), S(0xD801, 'A')), (S(0xD801, 'a'), S(0xD802, 'A')), (S(0xDC00), S(0xDC00)), (S(0xDC28), S(0xDC00)),
            (S(0xD801), S(0xD801)), (S('a', 0xD801), S('A', 0xD801)), (S(0x10400, 'a'), S(0x10428, 'A')), (S(0xD801, 0x10400), S(0xD801, 0x10428)),
            (S(0x10400), "ab")];
        foreach (var pair in chosen) yield return pair;
        string[] tokens = ["a", "A", "s", "S", "k", "K", "i", "I", "x", "_", .. TokenCodes.Select(code => S(code))];
        var random = new Random(20261014);
        for (var index = 0; index < 300; index++)
        {
            string left = "", right = "";
            for (var count = random.Next(1, 5); count > 0; count--)
            {
                var token = tokens[random.Next(tokens.Length)];
                var equal = tokens.Where(other => string.Equals(other, token, StringComparison.OrdinalIgnoreCase)).ToArray();
                left += token;
                right += random.Next(10) < 7 ? equal[random.Next(equal.Length)] : tokens[random.Next(tokens.Length)];
            }
            yield return (left, right);
        }
    }

    // Unicode 16.0、17.0 新增的大小写字母。OrdinalIgnoreCase 在 Linux 与 macOS 上按宿主 ICU 的大小写映射判断：Ubuntu 24.04 的 ICU 74（Unicode 15.1，
    // CI 与 .NET 10 默认镜像）不认这些字母，较新的 ICU 才把它们与对应字母视为相等。等价类表只收录各宿主一致的部分：这些码点从等价类里去掉，
    // CLI 把它们当作自成一类，同 ICU 74。宿主 ICU 再新增大小写对时，本测试会在该宿主上失败，把新字母补进这里。
    private static readonly HashSet<int> IcuDependentCase = [0x1C89, 0x1C8A, 0xA7CB, 0xA7CC, 0xA7CD, 0xA7CE, 0xA7CF, 0xA7D2, 0xA7D4, 0xA7DA, 0xA7DB, 0xA7DC,
        .. Enumerable.Range(0x10D50, 0x16), .. Enumerable.Range(0x10D70, 0x16)];

    // 按 StringComparer.OrdinalIgnoreCase 给全部码点分组（BMP 逐码元、增补平面逐码点），去掉随宿主 ICU 而异的字母，只列两个及以上成员的类。
    private static string CaseTable()
    {
        var groups = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (var code = 0; code <= 0x10FFFF; code++)
        {
            var text = code > char.MaxValue ? char.ConvertFromUtf32(code) : new string((char)code, 1);
            if (!groups.TryGetValue(text, out var members)) groups[text] = members = [];
            members.Add(code);
        }
        var lines = groups.Values.Select(members => members.Where(code => !IcuDependentCase.Contains(code)).ToList()).Where(members => members.Count > 1).OrderBy(members => members[0])
            .Select(members => "[" + string.Join(",", members.Select(code => "0x" + code.ToString("X4", CultureInfo.InvariantCulture))) + "]");
        return "// 由 tests/Qian.Common.Tests 的 SchemaPatternTests 按 .NET StringComparer.OrdinalIgnoreCase 生成，不要手改；重新生成见 packages/Common/README.md。\n" +
            "// 每项是一个含两个及以上码点的等价类，码点升序；BMP 码元与增补平面码点不会同类，未列出的码点自成一类。\n" +
            "// 只收录各宿主 ICU 一致的部分：Unicode 16.0 起新增的大小写字母（如 U+A7CB）随宿主 ICU 而异，这里不列，同 ICU 74 自成一类。\n" +
            "export const ordinalIgnoreCaseClasses:readonly (readonly number[])[]=[\n" + string.Join(",\n", lines) + "\n];\n";
    }
}
