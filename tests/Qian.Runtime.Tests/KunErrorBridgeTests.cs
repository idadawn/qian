using System.Xml.Linq;
using Kun.Core.Domain;
using Kun.Core.Results;
using Qian.Runtime.Errors;

namespace Qian.Runtime.Tests;

public sealed class KunErrorBridgeTests
{
    [Theory]
    [InlineData(ErrorType.Validation, "INPUT_INVALID", 400)]
    [InlineData(ErrorType.NotFound, "NOT_FOUND", 404)]
    [InlineData(ErrorType.Conflict, "CONFLICT", 409)]
    [InlineData(ErrorType.Unauthorized, "UNAUTHENTICATED", 401)]
    [InlineData(ErrorType.Forbidden, "AUTHZ_DENIED", 403)]
    [InlineData(ErrorType.Unavailable, "UNAVAILABLE", 503)]
    [InlineData(ErrorType.Failure, "INTERNAL", 500)]
    public void Maps_Kun_types_without_exposing_raw_code_or_message(ErrorType type, string code, int status)
    {
        var error = new Error("order.privateCode", "private message", type).ToQianError();
        Assert.Equal(code, error.Code.Code);
        Assert.Equal(status, error.Code.Status);
        Assert.Empty(error.Args);
        Assert.Empty(error.Errors);
        Assert.True(ErrorCatalog.CreateDefault().Contains(error.Code));
    }

    [Fact]
    public void Success_is_not_an_error_and_domain_failures_use_a_business_code()
    {
        Assert.Throws<ArgumentException>(() => Error.None.ToQianError());
        Assert.Same(PlatformErrors.BusinessRuleViolated, new SampleDomainException().ToQianError().Code);
    }

    [Fact]
    public void Runtime_dependency_boundary_and_Kun_versions_are_fixed()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var project = XDocument.Load(Path.Combine(root.FullName, "packages/Qian.Runtime/Qian.Runtime.csproj"));
        Assert.Equal(["Kun.Core", "Kun.Mediator", "Microsoft.Extensions.DependencyInjection"],
            project.Descendants("PackageReference").Select(e => (string)e.Attribute("Include")!).Order(StringComparer.Ordinal));
        Assert.EndsWith("Qian.Runtime.Errors.csproj", Assert.Single(project.Descendants("ProjectReference")).Attribute("Include")!.Value, StringComparison.Ordinal);
        var versions = XDocument.Load(Path.Combine(root.FullName, "Directory.Packages.props"));
        Assert.Single(versions.Descendants("KunVersion"));
        Assert.All(versions.Descendants("PackageVersion").Where(e => e.Attribute("Include")!.Value.StartsWith("Kun.", StringComparison.Ordinal)),
            package => Assert.Equal("[$(KunVersion)]", package.Attribute("Version")!.Value));
        using var assets = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, "packages/Qian.Runtime/obj/project.assets.json")));
        var kunVersion = versions.Descendants("KunVersion").Single().Value;
        Assert.All(assets.RootElement.GetProperty("libraries").EnumerateObject().Where(p => p.Name.StartsWith("Kun.", StringComparison.Ordinal)), package =>
        {
            Assert.Equal(kunVersion, package.Name.Split('/')[1]);
            Assert.Equal("package", package.Value.GetProperty("type").GetString());
        });
        Assert.DoesNotContain(assets.RootElement.GetProperty("libraries").EnumerateObject(), p => p.Name.StartsWith("Kun.Validation/", StringComparison.Ordinal));
        var nuget = XDocument.Load(Path.Combine(root.FullName, "NuGet.config"));
        Assert.Equal("https://api.nuget.org/v3/index.json", Assert.Single(nuget.Descendants("packageSources").Elements("add")).Attribute("value")!.Value);
        Assert.Contains(nuget.Descendants("packageSource").Single(e => e.Attribute("key")!.Value == "nuget.org").Elements("package"), p => p.Attribute("pattern")!.Value == "Kun.*");
    }

    private sealed class SampleDomainException() : DomainException("private.domain", "private message");
}
