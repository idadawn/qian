using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Capabilities;
using Qian.Capabilities.AspNetCore;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Tenancy.Signup.Tests;

// 组件 HTTP 契约由真实端点导出（两组端点同一份文档），与 plugins/qian.tenancy.signup/contracts/http-v1.openapi.json 比对漂移；
// QIAN_UPDATE_SIGNUP_CONTRACT=1 时重写发布契约。
public sealed class SignupContractTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly JsonSerializerOptions ContractJson = new() { WriteIndented = true };

    internal static async Task<JsonElement> CompileAsync()
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
        builder.Services.AddAuthorization(); builder.Services.AddQianErrors(); builder.Services.AddQianHttpContracts();
        // 与参考运营宿主相同的 JSON 绑定口径：属性名区分大小写、构造参数必填、可空注解生效。
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNameCaseInsensitive = false;
            options.SerializerOptions.RespectRequiredConstructorParameters = true;
            options.SerializerOptions.RespectNullableAnnotations = true;
        });
        await using var app = builder.Build();
        app.MapQianTenancySignupApplicant(_ => throw new InvalidOperationException("契约编译不会执行端点。"));
        app.MapQianTenancySignupOperations(_ => throw new InvalidOperationException("契约编译不会执行端点。"));
        await app.StartAsync(Ct);
        try { return await app.Services.GetRequiredService<IHttpContractSource>().CompileDocumentAsync(TenancySignupDefinition.ComponentId, Ct); }
        finally { await app.StopAsync(Ct); }
    }

    [Fact]
    public async Task 契约由真实端点导出_申请人接口匿名_运营接口要求认证_请求体没有租户字段_发布契约无漂移()
    {
        var contract = await CompileAsync();
        var paths = contract.GetProperty("paths");
        string[] expected =
        [
            "/api/signup/applications", "/api/signup/applications/{id}", "/api/signup/applications/{id}:approve", "/api/signup/applications/{id}:reject",
            "/api/signup/applications/{id}:retry", "/api/signup/code-availability", "/api/signup/me/applications", "/api/signup/me/applications/{id}:withdraw",
            "/api/signup/meta/state-machine", "/api/signup/session", "/api/signup/verifications"
        ];
        Assert.Equal(expected, paths.EnumerateObject().Select(path => path.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["delete", "get", "post"], paths.GetProperty("/api/signup/session").EnumerateObject().Select(method => method.Name).Order(StringComparer.Ordinal));
        foreach (var path in paths.EnumerateObject())
            foreach (var operation in path.Value.EnumerateObject())
            {
                Assert.Equal(TenancySignupDefinition.ComponentId, operation.Value.GetProperty("x-qian-plugin").GetString());
                var anonymous = operation.Value.GetProperty("security").GetArrayLength() == 0;
                // 申请人接口（含本人的申请）公开、只认申请人会话 Cookie；运营接口一律要求宿主认证（Bearer）。
                Assert.Equal(TenancySignupEndpoints.IsApplicantPath(new Microsoft.AspNetCore.Http.PathString(path.Name)), anonymous);
            }
        var document = JsonNode.Parse(contract.GetRawText())!;
        foreach (var (path, method) in new[] { ("/api/signup/verifications", "post"), ("/api/signup/session", "post"), ("/api/signup/me/applications", "post"),
                     ("/api/signup/applications/{id}:approve", "post"), ("/api/signup/applications/{id}:reject", "post") })
        {
            var schema = Resolve(document, document["paths"]![path]![method]!["requestBody"]!["content"]!["application/json"]!["schema"]!);
            Assert.False(schema["additionalProperties"]!.GetValue<bool>());
            Assert.Empty(TenantInputContract.FindFields(JsonSerializer.SerializeToElement(schema), contract));
        }
        // 提交申请：共有字段与新账号字段都在契约里，规模与条款按取值约束，租户号按格式约束。
        var submit = Resolve(document, document["paths"]!["/api/signup/me/applications"]!["post"]!["requestBody"]!["content"]!["application/json"]!["schema"]!);
        Assert.Equal(["acceptTerms", "code", "companyName", "size"], submit["required"]!.AsArray().Select(item => item!.GetValue<string>()).Order(StringComparer.Ordinal));
        Assert.Equal(["acceptTerms", "code", "companyName", "description", "displayName", "email", "industry", "password", "size"],
            submit["properties"]!.AsObject().Select(item => item.Key).Order(StringComparer.Ordinal));
        Assert.Equal(SignupCompanySizes.All, submit["properties"]!["size"]!["enum"]!.AsArray().Select(item => item!.GetValue<string>()));
        // 驳回原因必填；通过的开通方式只有 trial / formal。
        var reject = Resolve(document, document["paths"]!["/api/signup/applications/{id}:reject"]!["post"]!["requestBody"]!["content"]!["application/json"]!["schema"]!);
        Assert.Equal(["reason"], reject["required"]!.AsArray().Select(item => item!.GetValue<string>()));
        Assert.Equal(500, reject["properties"]!["reason"]!["maxLength"]!.GetValue<int>());
        var approve = Resolve(document, document["paths"]!["/api/signup/applications/{id}:approve"]!["post"]!["requestBody"]!["content"]!["application/json"]!["schema"]!);
        Assert.Equal(["plan"], approve["required"]!.AsArray().Select(item => item!.GetValue<string>()));
        Assert.Equal(["formal", "trial"], Resolve(document, approve["properties"]!["plan"]!)["enum"]!.AsArray().Select(item => item!.GetValue<string>()).Order(StringComparer.Ordinal));
        // 发码 202、超限 429、没有发件端口 503；提交 201、密码策略 422。
        var verifications = paths.GetProperty("/api/signup/verifications").GetProperty("post").GetProperty("responses");
        Assert.True(verifications.TryGetProperty("202", out _) && verifications.TryGetProperty("429", out _) && verifications.TryGetProperty("503", out _));
        var submitted = paths.GetProperty("/api/signup/me/applications").GetProperty("post").GetProperty("responses");
        Assert.True(submitted.TryGetProperty("201", out _) && submitted.TryGetProperty("422", out _) && submitted.TryGetProperty("409", out _));

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        var file = Path.Combine(root!.FullName, "plugins/qian.tenancy.signup/contracts/http-v1.openapi.json");
        if (Environment.GetEnvironmentVariable("QIAN_UPDATE_SIGNUP_CONTRACT") == "1")
        { Directory.CreateDirectory(Path.GetDirectoryName(file)!); await File.WriteAllTextAsync(file, JsonSerializer.Serialize(contract, ContractJson) + "\n", Ct); }
        Assert.True(JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(file, Ct)), contract), "真实端点与发布契约漂移。");
    }

    internal static JsonNode Resolve(JsonNode document, JsonNode schema) =>
        schema["$ref"] is { } reference ? Resolve(document, document["components"]!["schemas"]![reference.GetValue<string>().Split('/')[^1]]!) : schema;
}
