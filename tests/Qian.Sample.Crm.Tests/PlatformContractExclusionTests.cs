using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Qian.Capabilities;
using Qian.Plugin.Sdk;
using Qian.Plugin.Sdk.AspNetCore;

namespace Qian.Sample.Crm.Tests;

public abstract partial class CustomerCommandTests
{
    private protected static void MapContractExclusion(WebApplication app)
    {
        foreach (var platform in new[] { false, true })
        {
            var endpoint = app.MapGet(platform ? "/api/exclusion/platform" : "/api/exclusion/tenant", () => Results.Ok("probe"))
                .WithMetadata(new PluginEndpointBinding("crm", "qian.sample.crm", CapabilityBindingRegistration.Key, "qian.sample.crm/customer.list"))
                .RequireAuthorization().Produces<string>();
            if (platform) endpoint.WithMetadata(new PlatformHttpContract("qian.sample.crm"));
        }
    }
}

public sealed partial class CapabilityTests
{
    [Fact]
    public async Task 平台双标记端点从租户契约排除_同绑定普通端点保留_构建契约均保留()
    {
        await using var host = await Start(capabilities: true, contractExclusion: true);
        using var response = await CatalogRequest(host, A, "/api/capabilities/openapi.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var visible = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("paths");
        Assert.True(visible.TryGetProperty("/api/exclusion/tenant", out _));
        Assert.False(visible.TryGetProperty("/api/exclusion/platform", out _));
        var complete = (await host.App.Services.GetRequiredService<IHttpContractSource>().CompileDocumentAsync("qian.sample.crm", Ct)).GetProperty("paths");
        Assert.True(complete.TryGetProperty("/api/exclusion/tenant", out _));
        Assert.True(complete.TryGetProperty("/api/exclusion/platform", out _));
    }
}
