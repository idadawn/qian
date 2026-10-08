using System.Net;
using System.Text.Json;
using Kun.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Runtime.Errors.Tests;

public sealed class KunValidationBridgeTests
{
    [Theory]
    [InlineData("Items[0].Name", "#/Items/0/Name")]
    [InlineData("a/b~c", "#/a~1b~0c")]
    [InlineData("", "#")]
    [InlineData("odd[not-an-index]", "#/odd%5Bnot-an-index%5D")]
    public void Property_paths_become_escaped_pointers(string property, string expectedLocation)
    {
        var error = new ValidationException([new(property, "REQUIRED", "private")]).ToQianError();
        Assert.Same(PlatformErrors.InputInvalid, error.Code);
        var field = Assert.Single(error.Errors);
        Assert.Equal(expectedLocation, field.Pointer);
        Assert.Same(PlatformErrors.Required, field.Code);
    }

    [Fact]
    public void Field_errors_remain_within_the_frozen_contract()
    {
        var error = new ValidationException(Enumerable.Range(0, 110).Select(i => new ValidationError($"Items[{i}]", "kun.custom", "private")).ToArray()).ToQianError();
        Assert.Equal(100, error.Errors.Count);
        Assert.All(error.Errors, e => Assert.Same(PlatformErrors.ValueNotAllowed, e.Code));
        var detail = ErrorCatalog.CreateDefault().CreateDetail(error, new string('a', 32));
        var body = JsonSerializer.SerializeToElement(detail, ErrorDetailJson.TypeInfo);
        Assert.Empty(Contracts.Validate("error-detail", body));
        Assert.Empty(Contracts.CheckSemantics("error-detail", body));
        Assert.Equal("#/Items/0", body.GetProperty("pointer").GetString());
    }

    [Fact]
    public async Task Real_validation_exception_response_is_normalized_before_a_catch_all_handler()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddExceptionHandler<CatchAllHandler>();
        builder.Services.AddQianErrors();
        builder.Services.AddQianErrors();
        await using var app = builder.Build();
        app.UseQianErrors();
        app.MapGet("/validate", string () => throw new ValidationException([
            new("Items[0].Name", "REQUIRED", "private message"),
            new("Amount", "OUT_OF_RANGE", "private message")
        ]));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync(new Uri("/validate", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorDetailJson.MediaType, response.Content.Headers.ContentType?.MediaType);
        var raw = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("private", raw, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(raw);
        var body = document.RootElement;
        Assert.Empty(Contracts.Validate("error-detail", body));
        Assert.Empty(Contracts.CheckSemantics("error-detail", body));
        Assert.Equal("INPUT_INVALID", body.GetProperty("code").GetString());
        Assert.Equal("#/Items/0/Name", body.GetProperty("pointer").GetString());
        Assert.Equal("OUT_OF_RANGE", body.GetProperty("errors")[1].GetProperty("code").GetString());
    }

    private sealed class CatchAllHandler : IExceptionHandler
    {
        public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            httpContext.Response.StatusCode = 599;
            return ValueTask.FromResult(true);
        }
    }
}
