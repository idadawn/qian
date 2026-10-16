using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Qian.Capabilities;
using Qian.Capabilities.AspNetCore;

namespace Qian.Sample.Crm.Tests;

public sealed class HttpSchemaDirectionTests
{
    [Fact]
    public async Task 同一DTO的DisallowNull仅收窄请求_响应保留可空读取类型()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddAuthorization(); builder.Services.AddQianHttpContracts();
        await using var app = builder.Build();
        app.MapPost("/probe", (NullableRead input) => Results.Ok(input))
            .WithMetadata(new PlatformHttpContract("qian.test")).Produces<NullableRead>();
        await app.StartAsync(TestContext.Current.CancellationToken);
        var contract = await app.Services.GetRequiredService<IHttpContractSource>().CompileDocumentAsync("qian.test", TestContext.Current.CancellationToken);
        var document = JsonNode.Parse(contract.GetRawText())!;
        var operation = document["paths"]!["/probe"]!["post"]!;
        var request = TenantContractSchemaTests.Resolve(document, operation["requestBody"]!["content"]!["application/json"]!["schema"]!);
        var response = TenantContractSchemaTests.Resolve(document, operation["responses"]!["200"]!["content"]!["application/json"]!["schema"]!);
        Assert.Equal("string", request["properties"]!["name"]!["type"]!.GetValue<string>());
        Assert.Contains(response["properties"]!["name"]!["type"]!.AsArray(), value => value!.GetValue<string>() == "null");
    }
    public sealed class NullableRead
    {
        [DisallowNull] public string? Name { get; set; }
    }
}
