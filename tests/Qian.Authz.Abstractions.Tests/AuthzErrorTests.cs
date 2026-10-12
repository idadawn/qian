using System.Globalization;
using System.Text.Json;
using Json.Schema;
using Qian.Runtime.Errors;

namespace Qian.Authz.Abstractions.Tests;

public sealed class AuthzErrorTests
{
    [Fact]
    public void Catalog_and_localized_details_conform_to_frozen_schemas()
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        var directory = Path.Combine(PermissionContractTests.Root, "contracts/v0");
        var detailSchema = JsonSchema.FromText(File.ReadAllText(Path.Combine(directory, "error-detail.schema.json")), options);
        var catalogSchema = JsonSchema.FromText(File.ReadAllText(Path.Combine(directory, "error-catalog.schema.json")), options);
        var catalog = ErrorCatalog.CreateDefault();
        var originalCount = catalog.Codes.Count;
        using var registration = AuthzErrors.Register(catalog);
        Assert.Equal(originalCount + 4, catalog.Codes.Count);
        Assert.Same(PlatformErrors.AuthzDenied, AuthzErrors.Denied);
        Assert.Equal(5, AuthzErrors.All.Select(e => e.Code).Distinct(StringComparer.Ordinal).Count());
        using var exported = JsonDocument.Parse(catalog.ExportJson());
        Assert.True(catalogSchema.Evaluate(exported.RootElement).IsValid);
        foreach (var code in AuthzErrors.All)
        {
            var args = code.Parameters.ToDictionary(p => p, _ => "5", StringComparer.Ordinal);
            foreach (var locale in new[] { "zh-CN", "en-US" })
            {
                var culture = CultureInfo.GetCultureInfo(locale);
                var error = code.Kind == ErrorKind.Field
                    ? new QianError(PlatformErrors.InputInvalid) { Errors = [new QianFieldError(code, "metadata/permissions.json#/0", args)] }
                    : new QianError(code, args);
                var detail = catalog.CreateDetail(error, new string('a', 32), culture);
                using var rendered = JsonDocument.Parse(ErrorDetailJson.Serialize(detail));
                Assert.True(detailSchema.Evaluate(rendered.RootElement).IsValid);
                Assert.Equal(locale == "en-US" ? "en" : "zh-CN", code.ResolveLocale(culture));
                Assert.DoesNotContain("{", detail.Message, StringComparison.Ordinal);
            }
        }
        registration.Dispose();
        registration.Dispose();
        Assert.Equal(originalCount, catalog.Codes.Count);
        Assert.True(catalog.Contains(PlatformErrors.AuthzDenied));
    }

    [Fact]
    public void Conflicting_catalog_registration_rolls_back_partial_work()
    {
        var catalog = ErrorCatalog.CreateDefault();
        var conflict = ErrorCode.Business("PARTIAL_SUCCESS", 422, "qian.other",
            new Dictionary<string, string> { ["zh-CN"] = "冲突" }, new Dictionary<string, string> { ["zh-CN"] = "冲突" });
        using var handle = catalog.Register(conflict);
        var before = catalog.ExportJson();
        Assert.Throws<InvalidOperationException>(() => AuthzErrors.Register(catalog));
        Assert.Equal(before, catalog.ExportJson());
        Assert.False(catalog.Contains(AuthzErrors.ConstraintMutex));
    }
}
