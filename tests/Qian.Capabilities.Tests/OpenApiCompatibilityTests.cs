using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qian.Capabilities.Tests;

public sealed class OpenApiCompatibilityTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static OasDiffOpenApiProbe Probe() => new(Environment.GetEnvironmentVariable("QIAN_OASDIFF_PATH")
        ?? throw new InvalidOperationException("先运行 scripts/prepare_contract_tools.py，并设置 QIAN_OASDIFF_PATH；不允许跳过真实兼容性检查。"));
    [Fact]
    public async Task 真实检查器允许兼容扩展并拒绝请求类型变化与端点删除()
    {
        var baseline = Document(); var compatible = Document();
        compatible["paths"]!["/customers"]!["post"]!["description"] = "更新说明";
        compatible["paths"]!["/customers"]!["post"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["properties"]!["label"] = new JsonObject { ["type"] = "string" };
        Assert.True((await Probe().CompareAsync(Json(baseline), Json(compatible), Ct)).IsCompatible);
        var breaking = Document();
        breaking["paths"]!["/customers"]!["post"]!["requestBody"]!["content"]!["application/json"]!["schema"]!["properties"]!["name"]!["type"] = "integer";
        var mismatch = await Probe().CompareAsync(Json(baseline), Json(breaking), Ct);
        Assert.False(mismatch.IsCompatible); Assert.NotEmpty(mismatch.BreakingRules);
        var removed = Document(); removed["paths"] = new JsonObject();
        Assert.False((await Probe().CompareAsync(Json(baseline), Json(removed), Ct)).IsCompatible);
    }
    [Fact]
    public async Task 外部引用和无效文档不能作为兼容结果()
    {
        var probe = Probe();
        var candidate = Document(); candidate["paths"]!["/customers"] = new JsonObject { ["$ref"] = "file:///untrusted-contract.json" };
        await Assert.ThrowsAnyAsync<Exception>(() => probe.CompareAsync(Json(Document()), Json(candidate), Ct).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => Probe().CompareAsync(Json(Document()), JsonSerializer.SerializeToElement(new { }), Ct).AsTask());
    }
    private static JsonElement Json(JsonNode value) => JsonSerializer.SerializeToElement(value);
    private static JsonObject Document() => JsonNode.Parse("""
        {"openapi":"3.1.1","info":{"title":"客户契约","version":"1.0.0"},"paths":{"/customers":{"post":{"operationId":"createCustomer","requestBody":{"required":true,"content":{"application/json":{"schema":{"type":"object","required":["name"],"properties":{"name":{"type":"string"}}}}}},"responses":{"200":{"description":"成功","content":{"application/json":{"schema":{"type":"object","required":["id"],"properties":{"id":{"type":"string"}}}}}}}}}}}
        """)!.AsObject();
}
