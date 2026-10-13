using System.Text.Json;
using Json.Schema;
using Kun.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Data.Dialect.Pg;

namespace Qian.Data.AspNetCore.Tests;

/// <summary>端点实际要求的权限点必须在 plugins/qian.data/metadata 声明，元数据符合 authz Schema，默认角色只授予声明过的权限点。</summary>
public sealed class DataPermissionMetadataTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public async Task 每个数据端点都要求已声明的权限点_默认角色只授予租户内权限()
    {
        var permissions = Read("permissions.json");
        var roles = Read("roles.json");
        Assert.True(Schema("authz-permissions.schema.json").Evaluate(permissions).IsValid);
        Assert.True(Schema("authz-roles.schema.json").Evaluate(roles).IsValid);
        var declared = permissions.EnumerateArray().SelectMany(item => item.GetProperty("actions").EnumerateArray()
            .Select(action => "qian.data:" + item.GetProperty("resource").GetString() + ":" + action.GetString())).ToHashSet(StringComparer.Ordinal);
        Assert.Equal([DataPermissions.ConnectionRead, DataPermissions.ConnectionTest, DataPermissions.ConnectionWrite, DataPermissions.TypeDelete, DataPermissions.TypeRead],
            declared.Order(StringComparer.Ordinal));

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        TestIdentity.Add(builder.Services);
        builder.Services.AddQianDataTypes().AddQianPgDialect();
        await using var app = builder.Build();
        app.MapQianData();
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();
        Assert.Equal(8, endpoints.Length);
        Assert.All(endpoints, endpoint => Assert.Contains(Assert.Single(endpoint.Metadata.GetOrderedMetadata<RequiresPermissionAttribute>()).Permission, declared));

        foreach (var role in roles.EnumerateArray())
        {
            Assert.StartsWith("tenant.", role.GetProperty("role").GetString(), StringComparison.Ordinal);
            foreach (var grant in role.GetProperty("grant").EnumerateArray().Select(item => item.GetString()!))
                Assert.Contains(declared, permission => grant.EndsWith(":*", StringComparison.Ordinal)
                    ? permission.StartsWith(grant[..^1], StringComparison.Ordinal) : permission == grant);
            // 删除基准类型属平台级操作，组件默认模板不授予任何租户角色。
            Assert.DoesNotContain(role.GetProperty("grant").EnumerateArray(), item => item.GetString()!.StartsWith("qian.data:type:", StringComparison.Ordinal)
                && item.GetString() != DataPermissions.TypeRead);
        }
    }

    private static JsonElement Read(string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "plugins", "qian.data", "metadata", name)));
        return document.RootElement.Clone();
    }

    private static JsonSchema Schema(string name) =>
        JsonSchema.FromText(File.ReadAllText(Path.Combine(Root, "schemas", "v2", name)), new BuildOptions { SchemaRegistry = new SchemaRegistry() });

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("找不到仓库根目录。");
    }
}
