using System.Reflection;
using System.Text.Json;
using Qian.Authz.Abstractions;
using Qian.Registry;
using Qian.Runtime.Errors;
using Qian.Surfaces;

namespace Qian.Authz.Tests;

public sealed class PermissionCatalogCompilerTests
{
    private static JsonElement Json(string value) => JsonSerializer.Deserialize<JsonElement>(value);
    private const string Plugin = "qian.sample.crm";
    private static JsonElement Permissions => Json("""[{"resource":"customer","actions":["read","write","export"],"risk":{"export":"high"},"stepUp":["export"],"terminals":{"export":["web"]}}]""");
    private static JsonElement Roles => Json("""[{"role":"tenant.owner","grant":["qian.sample.crm:customer:*"]},{"role":"tenant.user","grant":["qian.sample.crm:customer:read"]}]""");

    [Fact]
    public void 平台预装声明与真实属性声明编译结果相同_模板只展开本组件已知动作()
    {
        var compiler = new PermissionCatalogCompiler(new SurfaceValidator());
        var installed = new InstalledComponentMetadata(Plugin, "1.0.0", Json("{}"),
            new Dictionary<string, JsonElement> { ["custom/permissions.json"] = Permissions, ["custom/roles.json"] = Roles });
        var platform = compiler.FromInstalled(installed);
        var standalone = compiler.FromAttributes(Plugin, "1.0.0", typeof(CrmPermissions).GetCustomAttributes<PermissionAttribute>(), Roles);
        Assert.Equal(JsonSerializer.Serialize(platform), JsonSerializer.Serialize(standalone));
        Assert.Equal(3, platform.Permissions.Count);
        var export = Assert.Single(platform.Permissions, permission => permission.Action == "export");
        Assert.Equal(PermissionRisk.High, export.Risk); Assert.True(export.RequiresStepUp); Assert.Equal(["web"], export.Terminals);
        var read = Assert.Single(platform.Permissions, permission => permission.Action == "read");
        Assert.Equal(PermissionRisk.Low, read.Risk); Assert.False(read.RequiresStepUp);
        Assert.Equal(3, Assert.Single(platform.Roles, role => role.Role == "tenant.owner").Grants.Count);
        Assert.Equal([Plugin + ":customer:read"], Assert.Single(platform.Roles, role => role.Role == "tenant.user").Grants);
        Assert.All(platform.Roles.SelectMany(role => role.Grants), code => Assert.True(PermissionCode.IsValid(code)));
    }

    [Theory]
    [InlineData("qian.registry:scope:read")]
    [InlineData("*:customer:*")]
    [InlineData("qian.sample.crm:customer:delete")]
    [InlineData("qian.sample.crm:missing:*")]
    [InlineData("qian.sample.crm:*:read")]
    public void 角色模板不能授予外部或未知权限(string grant)
    {
        var roles = JsonSerializer.SerializeToElement(new[] { new { role = "tenant.owner", grant = new[] { grant } } });
        var error = Assert.Throws<QianErrorException>(() => new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata(Plugin, "1.0.0", Permissions, roles));
        Assert.Equal("INPUT_INVALID", error.Error.Code.Code);
        Assert.Equal("metadata/roles.json#/0/grant/0", Assert.Single(error.Error.Errors!).Pointer);
    }

    [Theory]
    [InlineData("[{\"resource\":\"customer\",\"actions\":[\"read\"],\"risk\":{\"write\":\"high\"}}]")]
    [InlineData("[{\"resource\":\"customer\",\"actions\":[\"read\"],\"stepUp\":[\"write\"]}]")]
    [InlineData("[{\"resource\":\"customer\",\"actions\":[\"read\"],\"terminals\":{\"write\":[\"web\"]}}]")]
    [InlineData("[{\"resource\":\"customer\",\"actions\":[\"read\"]},{\"resource\":\"customer\",\"actions\":[\"read\"]}]")]
    [InlineData("[{\"resource\":\"customer\",\"resource\":\"other\",\"actions\":[\"read\"]}]")]
    public void 重复声明和不存在的风险或终端动作被拒绝(string source)
    {
        var error = Assert.Throws<QianErrorException>(() => new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata(Plugin, "1.0.0", Json(source), Json("[]")));
        Assert.Equal("INPUT_INVALID", error.Error.Code.Code); Assert.NotEmpty(error.Error.Errors!);
    }

    [Fact]
    public void 非法风险值不会回显_平台角色不能由组件默认模板创建()
    {
        var compiler = new PermissionCatalogCompiler(new SurfaceValidator());
        var error = Assert.Throws<QianErrorException>(() => compiler.FromMetadata(Plugin, "1.0.0", Json("""[{"resource":"customer","actions":["read"],"risk":{"read":"DO-NOT-LEAK"}}]"""), Json("[]")));
        var catalog = ErrorCatalog.CreateDefault(); using var registration = AuthzErrors.Register(catalog);
        Assert.DoesNotContain("DO-NOT-LEAK", ErrorDetailJson.Serialize(catalog.CreateDetail(error.Error, new string('a', 32))), StringComparison.Ordinal);
        Assert.Throws<QianErrorException>(() => compiler.FromMetadata(Plugin, "1.0.0", Permissions, Json("""[{"role":"platform.superadmin","grant":["qian.sample.crm:customer:read"]}]""")));
    }

    [Fact]
    public void 自定义元数据目录的错误指针保持有效URI并不读取文件()
    {
        var compiler = new PermissionCatalogCompiler(new SurfaceValidator());
        var error = Assert.Throws<QianErrorException>(() => compiler.FromMetadata(Plugin, "1.0.0", Json("""[{"resource":"customer","actions":["read"],"stepUp":["missing"]}]"""), Json("[]"), "custom #目录/permissions.json"));
        Assert.Equal("custom%20%23%E7%9B%AE%E5%BD%95/permissions.json#/0", Assert.Single(error.Error.Errors!).Pointer);
    }

    [Permission("customer", "read")]
    [Permission("customer", "write")]
    [Permission("customer", "export", Risk = PermissionRisk.High, RequiresStepUp = true, Terminals = new[] { "web" })]
    private sealed class CrmPermissions;
}
