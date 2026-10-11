using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Kun.Authorization;

namespace Qian.Authz.Abstractions.Tests;

public sealed class PermissionContractTests
{
    [Theory]
    [InlineData("qian.sample.crm", "customer", "read")]
    [InlineData("qian.crm-v2", "customer_group", "bulk-write")]
    public void Qualified_names_agree_with_frozen_capability_contract(string plugin, string resource, string action)
    {
        var code = PermissionCode.Format(plugin, resource, action);
        Assert.Equal($"{plugin}:{resource}:{action}", code);
        Assert.True(PermissionCode.IsValid(code));
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "contracts/v0/capability-entry.schema.json")));
        var pattern = schema.RootElement.GetProperty("properties").GetProperty("requiredPermission").GetProperty("pattern").GetString()!;
        Assert.Matches(new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)), code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sample:customer:read")]
    [InlineData("Qian.crm:customer:read")]
    [InlineData("qian.crm:qian.crm.customer:read")]
    [InlineData("qian.crm:customer:")]
    [InlineData("qian.crm:customer:read:all")]
    [InlineData("qian.crm:customer:read\n")]
    [InlineData("qian.crm:customer:*")]
    public void Invalid_names_are_rejected(string? code) => Assert.False(PermissionCode.IsValid(code));

    [Fact]
    public void Metadata_roundtrip_preserves_short_resource_and_copies_terminal_list()
    {
        var terminals = new List<string> { "web", "mobile" };
        var declaration = new PermissionDefinition("qian.sample.crm", "customer", "export", PermissionRisk.High, true, terminals);
        terminals.Clear();
        var json = JsonSerializer.Serialize(declaration);
        Assert.Contains("\"Risk\":\"high\"", json, StringComparison.Ordinal);
        var copy = JsonSerializer.Deserialize<PermissionDefinition>(json)!;
        Assert.Equal("qian.sample.crm:customer:export", copy.Code);
        Assert.Equal("customer", copy.Resource);
        Assert.True(copy.RequiresStepUp);
        Assert.Equal(["web", "mobile"], copy.Terminals);
        Assert.Throws<ArgumentException>(() => PermissionCode.Format("qian.crm", "qian.crm:customer", "read"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PermissionDefinition("qian.crm", "customer", "read", (PermissionRisk)100, false, []));
        Assert.Throws<ArgumentException>(() => new PermissionDefinition("qian.crm", "customer", "read", PermissionRisk.Low, false, ["web", "web"]));
        Assert.Throws<ArgumentException>(() => new PermissionDefinition("qian.crm", "customer", "read", PermissionRisk.Low, false, [" "]));
    }

    [Fact]
    public void Definition_reuses_Kun_ports_and_has_only_permitted_direct_dependencies()
    {
        Assert.True(typeof(IPermissionChecker).IsAssignableFrom(typeof(IAuthzService)));
        Assert.Equal(typeof(IDataPermissionFilter), typeof(IAuthzService).GetProperty(nameof(IAuthzService.DataPermissions))!.PropertyType);
        Assert.Equal(typeof(IFieldPolicy), typeof(IAuthzService).GetProperty(nameof(IAuthzService.Fields))!.PropertyType);
        Assert.Equal(["BatchCheckAsync", "CheckAsync", "ExplainAsync", "ListAllowedActionsAsync", "ListPermissionsAsync"],
            typeof(IPermissionChecker).GetMethods().Select(m => m.Name).Order(StringComparer.Ordinal));
        var project = XDocument.Load(Path.Combine(Root, "packages/Qian.Authz.Abstractions/Qian.Authz.Abstractions.csproj"));
        Assert.Equal(["Kun.Authorization"], project.Descendants("PackageReference").Select(e => (string)e.Attribute("Include")!));
        Assert.Equal(["../Qian.Runtime/Qian.Runtime.csproj"], project.Descendants("ProjectReference").Select(e => (string)e.Attribute("Include")!));
    }

    internal static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("找不到仓库根目录。");
        }
    }
}
