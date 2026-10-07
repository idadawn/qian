using System.Globalization;
using System.Text.Json;

namespace Qian.Runtime.Errors.Tests;

/// <summary>错误码定义、目录登记、渲染与 JSON 导出。</summary>
public sealed class ErrorCatalogTests
{
    private const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";

    internal static readonly ErrorCode QuotaExceeded = ErrorCode.Business(
        "QUOTA_EXCEEDED", 422, "qian.files",
        Text("超出配额", "Quota exceeded"),
        Text("文件大小超出租户配额（剩余 {remaining} 字节）。", "The file exceeds the tenant quota ({remaining} bytes left)."));

    [Fact]
    public void Built_in_protocol_and_business_codes_use_disjoint_status_codes()
    {
        foreach (var code in PlatformErrors.All)
        {
            switch (code.Kind)
            {
                case ErrorKind.Protocol:
                    Assert.NotNull(code.Status);
                    Assert.False(ErrorCode.IsBusinessStatus(code.Status.Value), $"{code} 是协议错误却用了 {code.Status}");
                    Assert.Equal($"https://qian.dev/problems/protocol/{code.Code}", code.Type);
                    break;
                case ErrorKind.Business:
                    Assert.True(code.Status is 409 or 422, $"{code} 是业务错误却用了 {code.Status}");
                    Assert.Equal($"https://qian.dev/problems/business/{code.Code}", code.Type);
                    break;
                default:
                    Assert.Null(code.Status);
                    Assert.Null(code.Type);
                    break;
            }
            Assert.Equal(PlatformErrors.Owner, code.Owner);
            Assert.Equal(["en", "zh-CN"], code.Title.Keys);
            Assert.Equal(["en", "zh-CN"], code.Message.Keys);
        }
        Assert.Equal(PlatformErrors.All.Count, PlatformErrors.All.Select(c => c.Code).Distinct().Count());
    }

    [Theory]
    [InlineData(400, "BAD_REQUEST")]
    [InlineData(401, "UNAUTHENTICATED")]
    [InlineData(403, "AUTHZ_DENIED")]
    [InlineData(404, "NOT_FOUND")]
    [InlineData(409, "CONFLICT")]
    [InlineData(410, "BAD_REQUEST")]
    [InlineData(422, "BUSINESS_RULE_VIOLATED")]
    [InlineData(429, "RATE_LIMITED")]
    [InlineData(500, "INTERNAL")]
    [InlineData(501, "INTERNAL")]
    [InlineData(503, "UNAVAILABLE")]
    public void Status_fallback_maps_to_a_built_in_code(int status, string expected)
    {
        Assert.Equal(expected, PlatformErrors.ForStatus(status).Code);
    }

    [Fact]
    public void Export_conforms_to_catalog_schema_and_semantics()
    {
        var catalog = ErrorCatalog.CreateDefault();
        catalog.Register(QuotaExceeded);
        using var doc = JsonDocument.Parse(catalog.ExportJson());

        Assert.Empty(Contracts.Validate("error-catalog", doc.RootElement));
        Assert.Empty(Contracts.CheckSemantics("error-catalog", doc.RootElement));
        var quota = doc.RootElement.GetProperty("codes").EnumerateArray().Single(e => e.GetProperty("code").GetString() == "QUOTA_EXCEEDED");
        Assert.Equal("qian.files", quota.GetProperty("owner").GetString());
        Assert.Equal("The file exceeds the tenant quota ({remaining} bytes left).", quota.GetProperty("message").GetProperty("en").GetString());
    }

    /// <summary>
    /// contracts/v0/error-codes.json 是平台内置目录的冻结导出。改了 <see cref="PlatformErrors"/> 后
    /// 设置 QIAN_UPDATE_CONTRACTS=1 运行本测试重新生成，并在 PR 里说明变更。
    /// </summary>
    [Fact]
    public void Frozen_platform_catalog_matches_the_code()
    {
        var path = Path.Combine(Contracts.V0, "error-codes.json");
        var exported = ErrorCatalog.CreateDefault().ExportJson();
        if (Environment.GetEnvironmentVariable("QIAN_UPDATE_CONTRACTS") == "1")
            File.WriteAllText(path, exported);

        Assert.True(File.Exists(path), "缺少 contracts/v0/error-codes.json；以 QIAN_UPDATE_CONTRACTS=1 运行本测试生成。");
        Assert.Equal(File.ReadAllText(path), exported);
        using var frozen = Contracts.ReadJson(path);
        Assert.Empty(Contracts.Validate("error-catalog", frozen.RootElement));
        Assert.Empty(Contracts.CheckSemantics("error-catalog", frozen.RootElement));
    }

    [Fact]
    public void Registering_the_same_definition_is_shared_and_reference_counted()
    {
        var catalog = new ErrorCatalog();
        var first = catalog.Register(QuotaExceeded);
        var second = catalog.Register(ErrorCode.Business(
            "QUOTA_EXCEEDED", 422, "qian.files",
            Text("超出配额", "Quota exceeded"),
            Text("文件大小超出租户配额（剩余 {remaining} 字节）。", "The file exceeds the tenant quota ({remaining} bytes left).")));

        first.Dispose();
        first.Dispose();
        Assert.True(catalog.Contains(QuotaExceeded));
        second.Dispose();
        Assert.False(catalog.TryGet("QUOTA_EXCEEDED", out _));
    }

    [Fact]
    public void Registering_a_different_definition_for_the_same_code_fails()
    {
        var catalog = new ErrorCatalog();
        catalog.Register(QuotaExceeded);
        var other = ErrorCode.Business("QUOTA_EXCEEDED", 409, "qian.entitlement", Text("超出配额", "Quota exceeded"), Text("超出配额。", "Quota exceeded."));

        var ex = Assert.Throws<InvalidOperationException>(() => catalog.Register(other));
        Assert.Contains("qian.files", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Error_code_definitions_are_validated()
    {
        var title = Text("标题", "Title");
        var message = Text("文案。", "Message.");
        Assert.Throws<ArgumentException>(() => ErrorCode.Business("quota_exceeded", 422, "qian.files", title, message));
        Assert.Throws<ArgumentException>(() => ErrorCode.Business("QUOTA__EXCEEDED", 422, "qian.files", title, message));
        Assert.Throws<ArgumentException>(() => ErrorCode.Business("QUOTA_EXCEEDED", 422, "Files", title, message));
        Assert.Throws<ArgumentOutOfRangeException>(() => ErrorCode.Business("QUOTA_EXCEEDED", 400, "qian.files", title, message));
        Assert.Throws<ArgumentOutOfRangeException>(() => ErrorCode.Protocol("SOMETHING", 422, "qian.files", title, message));
        Assert.Throws<ArgumentOutOfRangeException>(() => ErrorCode.Protocol("SOMETHING", 302, "qian.files", title, message));
        Assert.Throws<ArgumentException>(() => ErrorCode.Business("QUOTA_EXCEEDED", 422, "qian.files", new Dictionary<string, string> { ["en"] = "Title" }, message));
        Assert.Throws<ArgumentException>(() => ErrorCode.Business("QUOTA_EXCEEDED", 422, "qian.files", new Dictionary<string, string> { ["zh-CN"] = "标题", ["zh_TW"] = "標題" }, message));
        var drift = Assert.Throws<ArgumentException>(() => ErrorCode.Business("QUOTA_EXCEEDED", 422, "qian.files", title, Text("剩余 {remaining} 字节。", "Quota exceeded.")));
        Assert.Contains("占位符", drift.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("zh-CN", "超出配额")]
    [InlineData("en", "Quota exceeded")]
    [InlineData("en-US", "Quota exceeded")]
    [InlineData("fr-FR", "超出配额")]
    [InlineData("", "超出配额")]
    public void Text_falls_back_from_specific_to_neutral_language_then_zh_CN(string culture, string expected)
    {
        var info = culture.Length == 0 ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(culture);
        Assert.Equal(expected, QuotaExceeded.GetTitle(info));
    }

    [Fact]
    public void Business_error_renders_to_a_schema_valid_detail_with_pointer()
    {
        var catalog = ErrorCatalog.CreateDefault();
        catalog.Register(QuotaExceeded);
        var error = new QianError(QuotaExceeded, new Dictionary<string, string> { ["remaining"] = "1048576" })
        {
            Pointer = ErrorPointer.Input("sizeBytes"),
        };

        var detail = catalog.CreateDetail(error, TraceId, CultureInfo.GetCultureInfo("zh-CN"), "/api/files");

        Assert.Equal("QUOTA_EXCEEDED", detail.Code);
        Assert.Equal(422, detail.Status);
        Assert.Equal("#/sizeBytes", detail.Pointer);
        Assert.Equal("文件大小超出租户配额（剩余 1048576 字节）。", detail.Message);
        AssertConforms(detail);
    }

    [Fact]
    public void Field_errors_render_with_top_level_pointer_and_allowed_from_the_first_item()
    {
        var catalog = ErrorCatalog.CreateDefault();
        var error = new QianError(PlatformErrors.InputInvalid)
        {
            Errors =
            [
                new QianFieldError(PlatformErrors.ValueNotAllowed, ErrorPointer.Input("purpose")) { Allowed = AllowedValues.Of("attachment", "avatar", "export") },
                new QianFieldError(PlatformErrors.Required, ErrorPointer.Input("idempotencyKey")),
            ],
        };

        var detail = catalog.CreateDetail(error, TraceId, CultureInfo.GetCultureInfo("en"));

        Assert.Equal("#/purpose", detail.Pointer);
        Assert.Equal(3, detail.Allowed!.Count);
        Assert.Equal(2, detail.Errors!.Count);
        Assert.Equal("This member is required.", detail.Errors[1].Message);
        AssertConforms(detail);
    }

    [Fact]
    public void Misuse_is_rejected_at_the_call_site()
    {
        var catalog = ErrorCatalog.CreateDefault();

        Assert.Throws<ArgumentException>(() => new QianError(PlatformErrors.Required));
        Assert.Throws<ArgumentException>(() => new QianFieldError(PlatformErrors.NotFound, "#/id"));
        Assert.Throws<ArgumentException>(() => new QianError(QuotaExceeded));
        Assert.Throws<ArgumentException>(() => new QianError(PlatformErrors.Conflict) { Pointer = "/sizeBytes" });
        Assert.Throws<ArgumentException>(() => new QianError(PlatformErrors.Conflict) { Allowed = [] });
        Assert.Throws<InvalidOperationException>(() =>
            catalog.CreateDetail(new QianError(QuotaExceeded, new Dictionary<string, string> { ["remaining"] = "1" }), TraceId));
        Assert.Throws<ArgumentException>(() => catalog.CreateDetail(new QianError(PlatformErrors.Conflict), "not-a-trace-id"));
        Assert.Throws<ArgumentException>(() => catalog.CreateDetail(
            new QianError(PlatformErrors.InputInvalid)
            {
                Pointer = "#/a",
                Errors = [new QianFieldError(PlatformErrors.Required, "#/b")],
            },
            TraceId));
    }

    [Fact]
    public void Pointers_escape_per_rfc_6901_and_percent_encode()
    {
        Assert.Equal("#", ErrorPointer.Input());
        Assert.Equal("#/items/0/a~1b~0c", ErrorPointer.Input("items", 0, "a/b~c"));
        Assert.Equal("#/%E5%90%8D%E7%A7%B0/a%20b", ErrorPointer.Input("名称", "a b"));
        Assert.Equal(".qian-plugin/plugin.json#/runtime/nuget/0", ErrorPointer.InFile(".qian-plugin/plugin.json", "runtime", "nuget", 0));
        Assert.Throws<ArgumentException>(() => ErrorPointer.InFile("a b.json"));
        Assert.True(ErrorPointer.IsValid("#/a~1b"));
        Assert.False(ErrorPointer.IsValid("#/a~2b"));
        Assert.False(ErrorPointer.IsValid("/a"));
    }

    [Fact]
    public void Detail_round_trips_through_json_without_null_members()
    {
        var detail = ErrorCatalog.CreateDefault().CreateDetail(new QianError(PlatformErrors.Conflict), TraceId);

        var json = ErrorDetailJson.Serialize(detail);
        var back = ErrorDetailJson.Deserialize(json)!;

        Assert.DoesNotContain("null", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u", json, StringComparison.Ordinal);
        Assert.Equal(detail.Code, back.Code);
        Assert.Equal(detail.Message, back.Message);
        Assert.Equal(detail.TraceId, back.TraceId);
    }

    internal static void AssertConforms(ErrorDetail detail)
    {
        using var doc = JsonDocument.Parse(ErrorDetailJson.Serialize(detail));
        Assert.Empty(Contracts.Validate("error-detail", doc.RootElement));
        Assert.Empty(Contracts.CheckSemantics("error-detail", doc.RootElement));
    }

    internal static Dictionary<string, string> Text(string zh, string en) => new() { ["zh-CN"] = zh, ["en"] = en };
}
