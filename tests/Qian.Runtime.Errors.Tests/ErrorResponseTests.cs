using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Runtime.Errors.Tests;

/// <summary>
/// 内存宿主里的真实 HTTP 响应：业务错误与协议错误都写成符合 ErrorDetail Schema 的 problem+json，
/// 二者以 type 与状态码区分；框架自己产生的错误同样被规范化。
/// </summary>
public sealed class ErrorResponseTests : IAsyncLifetime
{
    private const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";

    private static readonly ErrorCode OrderLocked = ErrorCode.Business(
        "ORDER_LOCKED", 409, "qian.sample.crm",
        ErrorCatalogTests.Text("订单已锁定", "Order locked"),
        ErrorCatalogTests.Text("订单已进入审批，不能修改。", "The order is under approval and cannot be changed."));

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddQianErrors(catalog =>
        {
            catalog.Register(ErrorCatalogTests.QuotaExceeded);
            catalog.Register(OrderLocked);
        });
        builder.Services.AddAuthentication(TestAuthHandler.SchemeName).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
        builder.Services.AddAuthorization();
        builder.Services.AddRequestLocalization(o => o.SetDefaultCulture("zh-CN").AddSupportedCultures("zh-CN", "en").AddSupportedUICultures("zh-CN", "en"));

        _app = builder.Build();
        _app.UseRequestLocalization();   // 在 UseQianErrors 之外，错误文案才能按请求语言渲染
        _app.UseQianErrors();
        _app.UseAuthentication();
        _app.UseAuthorization();

        _app.MapPost("/files", (UploadRequest request) =>
        {
            if (request.SizeBytes > 1024)
            {
                throw new QianErrorException(new QianError(ErrorCatalogTests.QuotaExceeded, new Dictionary<string, string> { ["remaining"] = "1024" })
                {
                    Pointer = ErrorPointer.Input("sizeBytes"),
                });
            }
            return Results.Ok();
        });
        _app.MapPost("/validate", () => QianResults.Error(new QianError(PlatformErrors.InputInvalid)
        {
            Errors =
            [
                new QianFieldError(PlatformErrors.ValueNotAllowed, ErrorPointer.Input("purpose")) { Allowed = AllowedValues.Of("attachment", "avatar", "export") },
                new QianFieldError(PlatformErrors.Required, ErrorPointer.Input("idempotencyKey")),
            ],
        }));
        _app.MapGet("/orders/1", () => QianResults.Error(new QianError(OrderLocked)));
        _app.MapGet("/secure", () => "ok").RequireAuthorization();
        _app.MapGet("/forbidden", () => Results.Forbid()).RequireAuthorization();
        _app.MapGet("/boom", string () => throw new InvalidOperationException("Host=db;Password=s3cret"));
        _app.MapGet("/problem/409", () => Results.Problem(statusCode: 409, detail: "raw detail from somewhere"));
        _app.MapGet("/problem/coded", () => Results.Problem(statusCode: 409, extensions: new Dictionary<string, object?> { ["code"] = "ORDER_LOCKED", ["stack"] = "x" }));
        _app.MapGet("/problem/mismatched", () => Results.Problem(statusCode: 400, extensions: new Dictionary<string, object?> { ["code"] = "ORDER_LOCKED" }));

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task Business_error_thrown_from_a_handler_has_stable_code_and_pointer()
    {
        var body = await Send(HttpMethod.Post, "/files", HttpStatusCode.UnprocessableEntity, json: """{"sizeBytes": 4096}""");

        Assert.Equal("QUOTA_EXCEEDED", body.GetProperty("code").GetString());
        Assert.Equal("https://qian.dev/problems/business/QUOTA_EXCEEDED", body.GetProperty("type").GetString());
        Assert.Equal("#/sizeBytes", body.GetProperty("pointer").GetString());
        Assert.Equal("文件大小超出租户配额（剩余 1024 字节）。", body.GetProperty("message").GetString());
        Assert.Equal("/files", body.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task Business_conflict_returned_as_a_result_uses_409()
    {
        var body = await Send(HttpMethod.Get, "/orders/1", HttpStatusCode.Conflict);

        Assert.Equal("ORDER_LOCKED", body.GetProperty("code").GetString());
        Assert.False(body.TryGetProperty("pointer", out _));
    }

    [Fact]
    public async Task Input_validation_is_a_protocol_error_with_field_pointers()
    {
        var body = await Send(HttpMethod.Post, "/validate", HttpStatusCode.BadRequest, language: "en");

        Assert.Equal("INPUT_INVALID", body.GetProperty("code").GetString());
        Assert.StartsWith("https://qian.dev/problems/protocol/", body.GetProperty("type").GetString(), StringComparison.Ordinal);
        Assert.Equal("#/purpose", body.GetProperty("pointer").GetString());
        Assert.Equal(["attachment", "avatar", "export"], body.GetProperty("allowed").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("#/idempotencyKey", body.GetProperty("errors")[1].GetProperty("pointer").GetString());
        Assert.Equal("This member is required.", body.GetProperty("errors")[1].GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("GET", "/secure", null, HttpStatusCode.Unauthorized, "UNAUTHENTICATED")]
    [InlineData("GET", "/forbidden", "u-1", HttpStatusCode.Forbidden, "AUTHZ_DENIED")]
    [InlineData("GET", "/no-such-route", null, HttpStatusCode.NotFound, "NOT_FOUND")]
    [InlineData("GET", "/files", null, HttpStatusCode.MethodNotAllowed, "METHOD_NOT_ALLOWED")]
    [InlineData("GET", "/boom", null, HttpStatusCode.InternalServerError, "INTERNAL")]
    public async Task Framework_generated_errors_are_normalized_to_protocol_errors(string method, string path, string? user, HttpStatusCode status, string code)
    {
        var body = await Send(new HttpMethod(method), path, status, user: user);

        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.StartsWith("https://qian.dev/problems/protocol/", body.GetProperty("type").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Malformed_json_body_is_bad_request()
    {
        var body = await Send(HttpMethod.Post, "/files", HttpStatusCode.BadRequest, json: """{"sizeBytes": """);

        Assert.Equal("BAD_REQUEST", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Wrong_content_type_is_unsupported_media_type()
    {
        var body = await Send(HttpMethod.Post, "/files", HttpStatusCode.UnsupportedMediaType, text: "sizeBytes=1");

        Assert.Equal("UNSUPPORTED_MEDIA_TYPE", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Unhandled_exception_does_not_leak_its_message()
    {
        var response = await _client.GetAsync(new Uri("/boom", UriKind.Relative), TestContext.Current.CancellationToken);
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("s3cret", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Plain_problem_details_are_rewritten_into_the_contract()
    {
        var body = await Send(HttpMethod.Get, "/problem/409", HttpStatusCode.Conflict);

        Assert.Equal("CONFLICT", body.GetProperty("code").GetString());
        Assert.StartsWith("https://qian.dev/problems/business/", body.GetProperty("type").GetString(), StringComparison.Ordinal);
        Assert.NotEqual("raw detail from somewhere", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Problem_details_carrying_a_registered_code_keep_it()
    {
        var body = await Send(HttpMethod.Get, "/problem/coded", HttpStatusCode.Conflict);

        Assert.Equal("ORDER_LOCKED", body.GetProperty("code").GetString());
        Assert.False(body.TryGetProperty("stack", out _));
    }

    [Fact]
    public async Task Problem_details_whose_code_disagrees_with_the_status_fall_back_by_status()
    {
        var body = await Send(HttpMethod.Get, "/problem/mismatched", HttpStatusCode.BadRequest);

        Assert.Equal("BAD_REQUEST", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Messages_follow_the_request_language()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/secure");
        request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await ReadAndValidate(response, HttpStatusCode.Unauthorized);

        Assert.Equal("Unauthenticated", body.GetProperty("title").GetString());
        Assert.Equal("en", response.Content.Headers.ContentLanguage.Single());
    }

    [Fact]
    public async Task Trace_id_is_the_w3c_trace_id_of_the_request()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/orders/1");
        request.Headers.Add("traceparent", $"00-{TraceId}-00f067aa0ba902b7-01");
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await ReadAndValidate(response, HttpStatusCode.Conflict);

        Assert.Equal(TraceId, body.GetProperty("trace_id").GetString());
    }

    private async Task<JsonElement> Send(HttpMethod method, string path, HttpStatusCode expected, string? json = null, string? text = null, string? user = null, string? language = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (json is not null)
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        if (text is not null)
            request.Content = new StringContent(text, Encoding.UTF8, "text/plain");
        if (user is not null)
            request.Headers.Add(TestAuthHandler.UserHeader, user);
        if (language is not null)
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(language));
        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        return await ReadAndValidate(response, expected);
    }

    /// <summary>每个错误响应：状态码、媒体类型、Schema、语义规则，以及 body.status 与状态行一致。</summary>
    private static async Task<JsonElement> ReadAndValidate(HttpResponseMessage response, HttpStatusCode expected)
    {
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(expected == response.StatusCode, $"期望 {(int)expected}，实际 {(int)response.StatusCode}：{raw}");
        Assert.Equal(ErrorDetailJson.MediaType, response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(raw);
        var body = doc.RootElement.Clone();
        var schemaErrors = Contracts.Validate("error-detail", body);
        Assert.True(schemaErrors.Count == 0, $"不符合 error-detail.schema.json：{string.Join(" | ", schemaErrors)}\n{raw}");
        Assert.Empty(Contracts.CheckSemantics("error-detail", body));
        Assert.Equal((int)expected, body.GetProperty("status").GetInt32());
        return body;
    }

    private sealed record UploadRequest(long SizeBytes);

    private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string UserHeader = "X-Test-User";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var user))
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.ToString())], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
