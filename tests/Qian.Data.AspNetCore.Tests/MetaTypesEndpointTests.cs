using System.Net;
using System.Text.Json;
using Json.Schema;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Data.Dialect.Pg;
using Qian.Runtime.Errors;

namespace Qian.Data.AspNetCore.Tests;

/// <summary>不需要数据库：GET /api/data/meta/types 返回基准类型、方言与映射，系统类型不可删除，PG 映射拒绝越界的字段定义。</summary>
public sealed class MetaTypesEndpointTests : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        TestIdentity.Add(builder.Services);
        builder.Services.AddQianDataTypes();
        builder.Services.AddQianPgDialect();
        _app = builder.Build();
        TestIdentity.Use(_app);
        _app.MapQianDataTypes();
        await _app.StartAsync(Ct);
        _client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task 元数据端点返回基准类型方言与映射_符合目录Schema()
    {
        // 平台级主体（不带租户）也可以读取全局元数据。
        using var response = await _client.SendAsync(TestIdentity.Request(HttpMethod.Get, "/api/data/meta/types", "reader"), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var json = await response.Content.ReadAsStringAsync(Ct);
        using var body = JsonDocument.Parse(json);
        var schema = JsonSchema.FromText(DataContractSchemas.DataTypes, new BuildOptions { SchemaRegistry = new SchemaRegistry() });
        Assert.True(schema.Evaluate(body.RootElement).IsValid, json);
        var types = body.RootElement.GetProperty("types").EnumerateArray().ToArray();
        Assert.Equal(["int", "bigint", "decimal", "float", "datetime", "varchar", "text", "bool", "json", "uuid"],
            types.Select(type => type.GetProperty("code").GetString()));
        Assert.All(types, type => Assert.True(type.GetProperty("isSystem").GetBoolean()));
        var pg = Assert.Single(body.RootElement.GetProperty("dialects").EnumerateArray());
        Assert.Equal("pg", pg.GetProperty("code").GetString());
        Assert.Equal(5432, pg.GetProperty("defaultPort").GetInt32());
        var mappings = pg.GetProperty("mappings").EnumerateArray().ToDictionary(mapping => mapping.GetProperty("type").GetString()!);
        Assert.Equal(10, mappings.Count);
        Assert.Equal("character varying", mappings["varchar"].GetProperty("nativeType").GetString());
        Assert.Equal(10485760, mappings["varchar"].GetProperty("length").GetProperty("max").GetInt32());
        Assert.Equal(28, mappings["decimal"].GetProperty("precision").GetProperty("max").GetInt32());
        Assert.Equal("2147483647", mappings["int"].GetProperty("range").GetProperty("max").GetString());
        Assert.Equal("jsonb", mappings["json"].GetProperty("nativeType").GetString());
    }

    [Fact]
    public async Task 系统基准类型不可删除_未知类型404_无权限403_匿名401()
    {
        foreach (var code in new[] { "int", "varchar", "uuid" })
        {
            using var response = await _client.SendAsync(TestIdentity.Request(HttpMethod.Delete, "/api/data/meta/types/" + code, "admin"), Ct);
            await AssertErrorAsync(response, HttpStatusCode.Conflict, DataErrors.TypeProtected.Code);
        }
        using (var response = await _client.SendAsync(TestIdentity.Request(HttpMethod.Delete, "/api/data/meta/types/money", "admin"), Ct))
            await AssertErrorAsync(response, HttpStatusCode.NotFound, DataErrors.TypeNotFound.Code);
        using (var response = await _client.SendAsync(TestIdentity.Request(HttpMethod.Delete, "/api/data/meta/types/int", "reader"), Ct))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using (var response = await _client.SendAsync(TestIdentity.Request(HttpMethod.Get, "/api/data/meta/types", "nobody"), Ct))
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using (var response = await _client.SendAsync(TestIdentity.Request(HttpMethod.Get, "/api/data/meta/types", null), Ct))
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        // 删除被拒后目录不变。
        Assert.Equal(10, _app.Services.GetRequiredService<IDataTypeCatalog>().Snapshot().Types.Count);
    }

    [Fact]
    public void PG方言映射拒绝超出长度精度与区间的字段定义()
    {
        var catalog = _app.Services.GetRequiredService<IDataTypeCatalog>();
        Assert.Equal("character varying", catalog.MapField("pg", new DataFieldDefinition("varchar", Length: 10485760)).NativeType);
        Assert.Equal(new DataFieldMapping("decimal", "numeric", null, 28, 10), catalog.MapField("pg", new DataFieldDefinition("decimal", Precision: 28, Scale: 10)));
        Assert.Equal(6, catalog.MapField("pg", new DataFieldDefinition("datetime")).Precision);
        AssertRejected(catalog, new DataFieldDefinition("varchar", Length: 10485761), "#/length");
        AssertRejected(catalog, new DataFieldDefinition("decimal", Precision: 29), "#/precision");
        AssertRejected(catalog, new DataFieldDefinition("decimal", Precision: 4, Scale: 5), "#/scale");
        AssertRejected(catalog, new DataFieldDefinition("datetime", Precision: 9), "#/precision");
        AssertRejected(catalog, new DataFieldDefinition("int", Minimum: -2147483649m), "#/minimum");
        AssertRejected(catalog, new DataFieldDefinition("bigint", Maximum: 9223372036854775808m), "#/maximum");
        AssertRejected(catalog, new DataFieldDefinition("decimal", Precision: 6, Scale: 2, Maximum: 10000m), "#/maximum");
        AssertRejected(catalog, new DataFieldDefinition("bool", Length: 1), "#/length");
        var unknown = Assert.Throws<QianErrorException>(() => catalog.MapField("pg", new DataFieldDefinition("money"))).Error;
        Assert.Equal(DataErrors.FieldDefinitionInvalid, unknown.Code);
        Assert.Equal("#/type", Assert.Single(unknown.Errors).Pointer);
        Assert.Equal(10, unknown.Errors[0].Allowed!.Count);
        Assert.Equal(DataErrors.DialectNotFound, Assert.Throws<QianErrorException>(() => catalog.MapField("mysql", new DataFieldDefinition("int"))).Error.Code);
    }

    [Fact]
    public void 方言注册表同名独占_释放句柄后才能重新注册()
    {
        var registry = _app.Services.GetRequiredService<IDataDialectRegistry>();
        var conflict = Assert.Throws<QianErrorException>(() => registry.Register("pg", new PgDialect())).Error;
        Assert.Equal(PlatformErrors.RegistrationConflict, conflict.Code);
        Assert.Equal("Data.Dialects:pg", conflict.Args["key"]);
        Assert.Throws<QianDataMisconfiguredException>(() => registry.Register("postgres", new PgDialect()));
        var isolated = new ServiceCollection().AddQianDataTypes().BuildServiceProvider().GetRequiredService<IDataDialectRegistry>();
        Assert.Empty(isolated.List());
        var lease = isolated.Register("pg", new PgDialect());
        Assert.NotNull(isolated.Find("pg"));
        lease.Dispose();
        Assert.Null(isolated.Find("pg"));
        isolated.Register("pg", new PgDialect()).Dispose();
    }

    private static void AssertRejected(IDataTypeCatalog catalog, DataFieldDefinition field, string pointer)
    {
        var error = Assert.Throws<QianErrorException>(() => catalog.MapField("pg", field)).Error;
        Assert.Equal(DataErrors.FieldDefinitionInvalid, error.Code);
        Assert.Contains(error.Errors, item => item.Pointer == pointer);
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }
}
