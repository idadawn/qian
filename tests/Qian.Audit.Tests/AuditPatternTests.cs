using System.Text.RegularExpressions;

namespace Qian.Audit.Tests;

public sealed class AuditPatternTests
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

    private static Regex Compile(string pattern) => new(AuditPattern.Translate(pattern), RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
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
            Assert.False(AuditPattern.IsStable("key" + (char)code));
        foreach (var token in new[] { @"\w", @"\W", @"\d", @"\D", @"\s", @"\S", "." })
        {
            var ours = Compile($"^{token}$"); var validator = Validator($"^{token}$");
            for (var code = 0; code <= char.MaxValue; code++)
            {
                var text = ((char)code).ToString();
                if (!AuditPattern.IsStable(text)) continue;
                var expected = Ecma(token, (char)code);
                if (ours.IsMatch(text) != expected || validator.IsMatch(text) != expected) Assert.Fail($"{token} 在 U+{code:X4} 上方言不一致");
            }
        }
    }

    [Fact]
    public void 方言一致的键名上_翻译结果与ECMA262固定向量和验证器方言逐例一致()
    {
        Assert.All(Keys, key => Assert.True(AuditPattern.IsStable(key)));
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
            "[a-z-[aeiou]]", "[]a]", "[^]", @"[^\D]", @"[^\W_]", @"[^\S]", @"[\d-z]", @"[a-\d]", "[a-b-c]", "[a--]", "[abc", @"[\b]",
            "a{", "a{1", "a{,2}", "a{x}", "}", "]", "*a", "^*", "$+", "(*a)", "a|*", "a**", "a*??", "a{2}{3}",
            "😀", "[😀]", @"\" + "uD83D", @"[\" + "uDE00]"];
        foreach (var pattern in rejected) Assert.Throws<NotSupportedException>(() => AuditPattern.Translate(pattern));
    }
}
