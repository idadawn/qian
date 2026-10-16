using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Json.Schema;
using Kun.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Runtime;

namespace Qian.Sample.Crm.Tests;

public sealed partial class CustomerCommandTests
{
    [Fact]
    public async Task 真实认证meta端点返回可离线使用的契约_匿名拒绝_卸载隐藏_数据执行仍缺省拒绝()
    {
        await using var host = await Start(humanOnlyProbe: new HumanOnlyProbe());
        using var anonymous = await host.Client.GetAsync("/api/authz/meta", Ct); Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var agentRequest = new HttpRequestMessage(HttpMethod.Get, "/api/authz/meta");
        agentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(A, "crm.read", Writer, "agent"));
        using var agent = await host.Send(agentRequest); Assert.Equal(HttpStatusCode.Forbidden, agent.StatusCode);
        using var response = await host.Get(A, "/api/authz/meta"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl is { Private: true, NoStore: true });
        var metadata = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        JsonSchema.FromText(metadata.GetProperty("filterAst").GetRawText(), options);
        var schema = JsonSchema.FromText(metadata.GetProperty("dataScopeRule").GetRawText(), options);
        var metaSchema = Assert.IsType<JsonSchema>(options.SchemaRegistry.Get(new Uri("https://json-schema.org/draft/2020-12/schema")));
        Assert.True(metaSchema.Evaluate(metadata.GetProperty("dataScopeRule")).IsValid);
        Assert.True(metaSchema.Evaluate(metadata.GetProperty("filterAst")).IsValid);
        Assert.True(schema.Evaluate(JsonSerializer.SerializeToElement(new { resource = "qian.sample.crm:customer", allData = true })).IsValid);
        Assert.True(JsonElement.DeepEquals(JsonDocument.Parse(DataContractSchemas.SystemVariables).RootElement, metadata.GetProperty("systemVariables")));
        Assert.Equal("and", metadata.GetProperty("merge").GetProperty("withinRole").GetString());
        Assert.Equal("or", metadata.GetProperty("merge").GetProperty("acrossRoles").GetString());
        var runtime = host.App.Services.GetRequiredService<CrmCommandRuntime>();
        var authz = runtime.Loader.GetContext("authz", ScopeKey.Global).Authz();
        var expression = await authz.DataPermissions.GetExpressionAsync<object>(new PermissionRequest(new AuthorizationSubject(Writer, A, "service"), "qian.sample.crm:customer:read"), Ct);
        Assert.False(expression.Compile()(new object()));
        await runtime.Loader.UnmountAsync("authz", ScopeKey.Global, Ct);
        using var gone = await host.Get(A, "/api/authz/meta"); Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }
}
