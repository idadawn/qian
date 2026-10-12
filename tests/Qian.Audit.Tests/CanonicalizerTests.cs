using System.Text.Json;
using Qian.Runtime.Errors;

namespace Qian.Audit.Tests;

public sealed class CanonicalizerTests
{
    internal static JsonElement Json(string value) => JsonSerializer.Deserialize<JsonElement>(value);
    internal static readonly string Tenant = "01900000-0000-7000-8000-000000000001";
    internal static AuditRecord Record(string? tenant = null, Guid? id = null, string payload = "{}", string actorType = "user", string? onBehalfOf = null) =>
        new(id ?? Guid.CreateVersion7(), tenant ?? Tenant, actorType, "actor", onBehalfOf, "crm.customer.create", "1.0.0",
            new string('1', 64), "api", "crm.customer:1", "success", new string('2', 32),
            new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero).AddTicks(1234567), Json(payload));

    [Fact]
    public void 全字段哈希匹配独立Python计算的固定向量()
    {
        var record = Record(id: Guid.Parse("01900000-0000-7000-8000-000000000003"), payload: "{\"alpha\":1000,\"beta\":[true,null,\"x\"]}");
        Assert.Equal("52258afa98a1f67b61340646e287d1949589d0d13575bba07f2cf6804b499172", AuditCanonicalizer.Hash(record, 7, new string('a', 64)));
    }

    [Fact]
    public void 规范化精确数字_键顺序与Unicode转义不影响哈希_保留大整数()
    {
        var id = Guid.CreateVersion7();
        var first = Record(id: id, payload: "{\"z\":1e3,\"a\":{\"值\":-0.0,\"huge\":9007199254740993}}");
        var second = Record(id: id, payload: "{\"a\":{\"huge\":9007199254740993,\"\\u503c\":0},\"z\":1000.00}");
        Assert.Equal(AuditCanonicalizer.Hash(first, 1, null), AuditCanonicalizer.Hash(second, 1, null));
        Assert.Equal("9007199254740993", AuditCanonicalizer.Canonicalize(Json("9007199254740993")));
        Assert.Equal("0.00123", AuditCanonicalizer.Canonicalize(Json("123e-5")));
        Assert.Equal("-12300", AuditCanonicalizer.Canonicalize(Json("-1.2300e4")));
        Assert.NotEqual(AuditCanonicalizer.Hash(first, 1, null), AuditCanonicalizer.Hash(first, 2, null));
        Assert.NotEqual(AuditCanonicalizer.Hash(first, 1, null), AuditCanonicalizer.Hash(first, 1, new string('a', 64)));
    }

    [Fact]
    public void 拒绝重复键与过大指数_变动补充字段和委托主体改变哈希()
    {
        Assert.Throws<QianErrorException>(() => AuditCanonicalizer.Canonicalize(Json("{\"x\":1,\"x\":2}")));
        Assert.Throws<QianErrorException>(() => AuditCanonicalizer.Canonicalize(Json("1e1001")));
        var id = Guid.CreateVersion7(); var original = Record(id: id, payload: "{\"bypass\":false}");
        Assert.NotEqual(AuditCanonicalizer.Hash(original, 1, null), AuditCanonicalizer.Hash(Record(id: id, payload: "{\"bypass\":true}"), 1, null));
        Assert.NotEqual(AuditCanonicalizer.Hash(original, 1, null), AuditCanonicalizer.Hash(Record(id: id, onBehalfOf: "user1", payload: "{\"bypass\":false}"), 1, null));
        Assert.Equal(0, original.OccurredAt.UtcTicks % 10); Assert.DoesNotContain("actor", original.ToString());
    }
}
