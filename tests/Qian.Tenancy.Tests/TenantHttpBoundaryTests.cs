using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Kun.Core.Domain;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Qian.Data;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;
using Qian.Testing.Containers;

namespace Qian.Tenancy.Tests;

// 真实 Kestrel、JwtBearer 验签与受 RLS 约束的应用角色；租户范围只由 TenantRequestBoundary 从令牌建立。
public sealed class TenantHttpBoundaryTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Guid A = Guid.Parse("01900000-0000-7000-8000-00000000000a");
    private static readonly Guid B = Guid.Parse("01900000-0000-7000-8000-00000000000b");

    [Fact]
    public async Task 真实HTTP_A的令牌请求B的记录id返回404_与不存在无法区分_请求头不能切换租户()
    {
        using var rsa = RSA.Create(2048); var calls = new ConcurrentQueue<string>();
        await using var app = Build(rsa, calls); await Prepare(app);
        var recordA = await Seed(app, A, "a 的记录"); var recordB = await Seed(app, B, "b 的记录");
        await app.StartAsync(Ct);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

        using (var own = await Get(client, recordA, Token(rsa, A)))
        {
            Assert.Equal(HttpStatusCode.OK, own.StatusCode);
            Assert.Equal(new Record(recordA, A.ToString(), "a 的记录"), await own.Content.ReadFromJsonAsync<Record>(Ct));
        }
        var unknown = await Error(await Get(client, Guid.CreateVersion7(), Token(rsa, A)), HttpStatusCode.NotFound, "NOT_FOUND");
        var crossAb = await Error(await Get(client, recordB, Token(rsa, A)), HttpStatusCode.NotFound, "NOT_FOUND");
        var crossBa = await Error(await Get(client, recordA, Token(rsa, B)), HttpStatusCode.NotFound, "NOT_FOUND");
        Assert.Equal(unknown.Message, crossAb.Message); Assert.Equal(unknown.Message, crossBa.Message);
        // X-Tenant-Id 不参与解析：A 的令牌带 B 的请求头，仍只在 A 的范围内。
        await Error(await Get(client, recordB, Token(rsa, A), header: B), HttpStatusCode.NotFound, "NOT_FOUND");
        using (var own = await Get(client, recordA, Token(rsa, A), header: B)) Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal([A.ToString(), A.ToString(), A.ToString(), B.ToString(), A.ToString(), A.ToString()], calls);

        // 无令牌只带请求头、令牌缺 tenant_id、签名被篡改：边界拒绝，处理器不执行。
        foreach (var token in new[] { null, Token(rsa, null), Token(rsa, B, tamper: true) })
            await Error(await Get(client, recordB, token, header: B), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        Assert.Equal(6, calls.Count);
        await app.StopAsync(Ct);
    }

    private WebApplication Build(RSA rsa, ConcurrentQueue<string> calls)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext()); builder.Services.AddKunEventBus();
        builder.Services.AddQianData<BoundaryRecordsDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(BoundaryRecordsDbContext.Schema));
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = "https://fixture.invalid/",
                ValidateAudience = true, ValidAudience = "tenancy-http", ValidateLifetime = true, ClockSkew = TimeSpan.Zero,
                ValidateIssuerSigningKey = true, IssuerSigningKey = new RsaSecurityKey(rsa), ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
            };
        });
        builder.Services.AddAuthorization(); builder.Services.AddQianErrors();
        var app = builder.Build();
        var boundary = new TenantRequestBoundary(app.Services.GetRequiredService<ITenantContext>());
        app.UseQianErrors(); app.UseAuthentication();
        app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"), branch => branch.Use(boundary.InvokeAsync));
        app.UseAuthorization();
        app.MapGet("/api/records/{id:guid}", async (Guid id, ITenantContext tenant, IDataService data, CancellationToken ct) =>
        {
            calls.Enqueue(tenant.TenantId!);
            await using var session = await data.OpenAsync<BoundaryRecordsDbContext>(ct);
            var row = await session.Context.Records.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            return row is null ? (IResult)QianResults.Error(new QianError(PlatformErrors.NotFound)) : Results.Ok(new Record(row.Id, row.TenantId, row.Text));
        }).RequireAuthorization();
        return app;
    }

    private static async Task<HttpResponseMessage> Get(HttpClient client, Guid id, string? token, Guid? header = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/records/" + id);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (header is not null) request.Headers.Add("X-Tenant-Id", header.Value.ToString());
        return await client.SendAsync(request, Ct);
    }

    private static async Task<ErrorDetail> Error(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            var detail = ErrorDetailJson.Deserialize(await response.Content.ReadAsStringAsync(Ct)); Assert.NotNull(detail);
            Assert.Equal(code, detail.Code); Assert.Equal((int)status, detail.Status);
            return detail;
        }
    }

    private static string Token(RSA rsa, Guid? tenant, bool tamper = false)
    {
        var claims = new List<Claim> { new("sub", "tenancy-http-user") }; if (tenant is not null) claims.Add(new("tenant_id", tenant.Value.ToString()));
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("https://fixture.invalid/", "tenancy-http", claims,
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(2), new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256)));
        if (!tamper) return token;
        var parts = token.Split('.'); var signature = Base64UrlEncoder.DecodeBytes(parts[2]); signature[0] ^= 1;
        return parts[0] + "." + parts[1] + "." + Base64UrlEncoder.Encode(signature);
    }

    private static async Task<Guid> Seed(WebApplication app, Guid tenant, string text)
    {
        using var scope = app.Services.GetRequiredService<ITenantContext>().BeginScope(tenant.ToString());
        await using var session = await app.Services.GetRequiredService<IDataService>().OpenAsync<BoundaryRecordsDbContext>(Ct);
        var row = new BoundaryRecord { Text = text }; session.Context.Records.Add(row); await session.SaveChangesAsync(Ct);
        Assert.Equal(tenant.ToString(), row.TenantId); return row.Id;
    }

    private async Task Prepare(WebApplication app)
    {
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<BoundaryRecordsDbContext>();
        var model = db.GetService<IDesignTimeModel>().Model;
        var operations = db.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel());
        var sql = string.Join('\n', db.GetService<IMigrationsSqlGenerator>().Generate(operations, model).Select(command => command.CommandText));
        Assert.Contains("CREATE POLICY qian_tenant_guard", sql, StringComparison.Ordinal);
        await using var admin = new NpgsqlConnection(postgres.ConnectionString); await admin.OpenAsync(Ct);
        await using var create = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {BoundaryRecordsDbContext.Schema} CASCADE;\n" + sql +
            $"\nGRANT USAGE ON SCHEMA {BoundaryRecordsDbContext.Schema} TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {BoundaryRecordsDbContext.Schema} TO qian_app;", admin);
        await create.ExecuteNonQueryAsync(Ct);
    }

    private sealed record Record(Guid Id, string TenantId, string Text);
}

internal sealed class BoundaryRecordsDbContext(DbContextOptions<BoundaryRecordsDbContext> options, ITenantContext tenant) : QianDbContext(options, tenant)
{
    public const string Schema = "plugin_qian_tenancy_boundary";
    public DbSet<BoundaryRecord> Records => Set<BoundaryRecord>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<BoundaryRecord>(entity =>
    {
        entity.ToTable("records"); entity.HasKey(x => x.Id);
        entity.Property(x => x.TenantId).HasMaxLength(36); entity.Property(x => x.Text).HasMaxLength(200).IsRequired();
    });
}

internal sealed class BoundaryRecord : IMultiTenant
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string TenantId { get; set; } = "";
    public string Text { get; set; } = "";
}
