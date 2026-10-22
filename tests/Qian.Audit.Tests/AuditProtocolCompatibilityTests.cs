using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Qian.Audit.Tests;

public sealed class AuditProtocolCompatibilityTests
{
    private static readonly JsonSerializerOptions Legacy = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Fact]
    public void 显式原生正文与历史反射序列化逐字节相同_兼容全部字段空值Unicode和时间精度()
    {
        var records = new[]
        {
            CanonicalizerTests.Record(id: Guid.Parse("01900000-0000-7000-8000-000000000003"), payload: "{\"alpha\":1000,\"beta\":[true,null,\"x\"]}"),
            new AuditRecord(Guid.NewGuid(), null, "service", "平台<>&", null, "qian.config/publish", "v1", new string('a', 64), "hook", null,
                "failure", new string('b', 32), new DateTimeOffset(2026, 10, 23, 12, 1, 2, TimeSpan.FromHours(5.5)), JsonElement.Parse("{\"中文\":{\"number\":9007199254740993,\"value\":-0.00}}")),
            new AuditRecord(Guid.NewGuid(), CanonicalizerTests.Tenant, "agent", "agent-1", "委托人", "qian.crm/create", "crm-v2", new string('1', 64), "mcp", "资源",
                "denied", new string('2', 32), new DateTimeOffset(2026, 10, 23, 1, 2, 3, TimeSpan.FromHours(-4)).AddTicks(1200001), JsonElement.Parse("{\"x\":[1e-3, true, null, \"\\u503c\"]}"))
        };
        foreach (var record in records)
        {
            var old = JsonSerializer.SerializeToNode(record, Legacy)!.AsObject(); old["seq"] = 17; old["format_version"] = "v1";
            var expected = AuditCanonicalizer.Canonicalize(JsonSerializer.SerializeToElement(old));
            Assert.Equal(expected, AuditProtocol.Content(record, 17));
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(new string('a', 64) + expected)));
            Assert.Equal(hash, AuditProtocol.Hash(record, 17, new string('a', 64)));
            Assert.Equal(hash, AuditCanonicalizer.Hash(record, 17, new string('a', 64)));
        }
    }

    [Fact]
    public void 旧输入策略入口与共享投影完全同源且保持关闭语义()
    {
        var schema = JsonElement.Parse("""{"type":"object","properties":{"name":{"type":"string"},"password":{"type":"string","sensitive":true}},"additionalProperties":true}""");
        var input = JsonElement.Parse("""{"name":"人","password":"secret","unknown":42}""");
        var legacy = new AuditInputPolicy(schema, true); var shared = new AuditInputProjection(schema, true);
        Assert.Equal(legacy.Redact(input).GetRawText(), shared.Redact(input).GetRawText()); Assert.Equal(legacy.InputHash(input), shared.InputHash(input));
        Assert.Null(new AuditInputPolicy(schema, true, false).InputHash(input)); Assert.Null(new AuditInputProjection(schema, true, false).InputHash(input));
    }
}
