using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Qian.Surfaces.Tests;

// S1-RT-020：schemaSha256 只有生产实现一份（ToolContractHash），这里直接用 RFC 8785 的正文示例与附录 B 的数值序列化样本断言它，
// 不在测试里另写 JCS。期望值取自 RFC 原文：把样本放进 inputSchema，期望的规范化文本即
// {"description":"d","inputSchema":<RFC 给出的输出>,"name":"n"}，摘要是它的 UTF-8 SHA-256。
public sealed class ToolContractHashTests
{
    [Fact]
    public void RFC8785正文示例_基本类型序列化与按UTF16码元排序成员()
    {
        // 基本类型一节的示例：数值、字符串转义与字面量。
        var primitives = "{\"numbers\":[333333333.33333329,1E30,4.50,2e-3,0.000000000000000000000000001]," +
            "\"string\":\"" + U("20ac") + "$" + U("000F") + U("000a") + "A'" + U("0042") + U("0022") + U("005c") + "\\\\" + "\\\"" + "\\/" + "\"," +
            "\"literals\":[null,true,false]}";
        var primitivesCanonical = "{\"literals\":[null,true,false],\"numbers\":[333333333.3333333,1e+30,4.5,0.002,1e-27]," +
            "\"string\":\"" + (char)0x20AC + "$" + U("000f") + "\\n" + "A'B" + "\\\"" + "\\\\" + "\\\\" + "\\\"" + "/\"}";
        Assert.Equal(Expected(primitivesCanonical), Hash(primitives));

        // 成员排序一节的示例：按 UTF-16 码元排序（U+1F600 的代理对排在 U+FB33 之前），非 ASCII 原样输出为 UTF-8。
        var sorting = "{\"" + U("20ac") + "\":\"Euro Sign\",\"\\r\":\"Carriage Return\",\"" + U("fb33") + "\":\"Hebrew Letter Dalet With Dagesh\"," +
            "\"1\":\"One\",\"" + U("d83d") + U("de00") + "\":\"Emoji: Grinning Face\",\"" + U("0080") + "\":\"Control\"," +
            "\"" + U("00f6") + "\":\"Latin Small Letter O With Diaeresis\"}";
        var sortingCanonical = "{\"\\r\":\"Carriage Return\",\"1\":\"One\",\"" + (char)0x80 + "\":\"Control\",\"" + (char)0xF6 + "\":\"Latin Small Letter O With Diaeresis\"," +
            "\"" + (char)0x20AC + "\":\"Euro Sign\",\"" + char.ConvertFromUtf32(0x1F600) + "\":\"Emoji: Grinning Face\",\"" + (char)0xFB33 + "\":\"Hebrew Letter Dalet With Dagesh\"}";
        Assert.Equal(Expected(sortingCanonical), Hash(sorting));
    }

    [Fact]
    public void RFC8785附录B数值序列化样本()
    {
        // NaN 与 Infinity（7fffffffffffffff、7ff0000000000000）在 JSON 文本里无法表示，解析阶段已拒绝，不在此列。
        var samples = new (string Bits, string Number)[]
        {
            ("0000000000000000", "0"), ("8000000000000000", "0"), ("0000000000000001", "5e-324"), ("8000000000000001", "-5e-324"),
            ("7fefffffffffffff", "1.7976931348623157e+308"), ("ffefffffffffffff", "-1.7976931348623157e+308"),
            ("4340000000000000", "9007199254740992"), ("c340000000000000", "-9007199254740992"), ("4430000000000000", "295147905179352830000"),
            ("44b52d02c7e14af5", "9.999999999999997e+22"), ("44b52d02c7e14af6", "1e+23"), ("44b52d02c7e14af7", "1.0000000000000001e+23"),
            ("444b1ae4d6e2ef4e", "999999999999999700000"), ("444b1ae4d6e2ef4f", "999999999999999900000"), ("444b1ae4d6e2ef50", "1e+21"),
            ("3eb0c6f7a0b5ed8c", "9.999999999999997e-7"), ("3eb0c6f7a0b5ed8d", "0.000001"),
            ("41b3de4355555553", "333333333.3333332"), ("41b3de4355555554", "333333333.33333325"), ("41b3de4355555555", "333333333.3333333"),
            ("41b3de4355555556", "333333333.3333334"), ("41b3de4355555557", "333333333.33333343"),
            ("becbf647612f3696", "-0.0000033333333333333333"), ("43143ff3c1cb0959", "1424953923781206.2"),
        };
        foreach (var (bits, number) in samples)
        {
            var value = BitConverter.Int64BitsToDouble(long.Parse(bits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
            Assert.True(Expected("{\"v\":" + number + "}") == Hash("{\"v\":" + value.ToString("R", CultureInfo.InvariantCulture) + "}"), bits + " → " + number);
        }
    }

    // 反斜杠与 u 分开写：JSON 文本里的 \uXXXX 转义由这里拼出。
    private static string U(string hex) => "\\" + "u" + hex;

    private static string Hash(string inputSchema)
    {
        using var tool = JsonDocument.Parse("{\"name\":\"n\",\"description\":\"d\",\"inputSchema\":" + inputSchema + "}");
        return ToolContractHash.Compute(tool.RootElement);
    }

    private static string Expected(string canonicalInputSchema) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("{\"description\":\"d\",\"inputSchema\":" + canonicalInputSchema + ",\"name\":\"n\"}")));
}
