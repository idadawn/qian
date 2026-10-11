using System.Security.Claims;
using Kun.AspNetCore.Authorization;
using Kun.Authorization;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.MultiTenancy.DependencyInjection;
using Kun.MultiTenancy.Middleware;
using Kun.Security.Authentication;
using Kun.Security.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Qian.Data;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Sample.Crm;

/// <summary>参考应用的数据入口；只接受指定内部服务的有效租户 S2S 令牌。</summary>
public static class CrmApplication
{
    public const string ListQueryTag = "qian.sample.crm.customer-list";

    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);
        var connection = builder.Configuration.GetConnectionString("Crm")
            ?? throw new InvalidOperationException("缺少 ConnectionStrings:Crm。");
        var issuer = Required("S2S:Issuer");
        var audience = Required("S2S:Audience");
        var clientId = Required("S2S:ReaderClientId");
        var keys = new JsonWebKeySet(Required("S2S:PublicJwks"));
        if (keys.Keys.Count == 0 || keys.Keys.Any(key => key.HasPrivateKey || key.Kty is not ("RSA" or "EC")))
            throw new InvalidOperationException("S2S:PublicJwks 必须只包含非对称公钥。");
        builder.Services.AddQianErrors();
        builder.Services.AddKunSecurity();
        builder.Services.AddKunMultiTenancy(options => { options.Production = true; options.FallbackTenantId = null; });
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddKunServiceAuthentication(options =>
        {
            options.Issuer = issuer;
            options.Audience = audience;
            options.StaticKeySet = keys;
        });
        builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options => options.MapInboundClaims = false);
        builder.Services.AddAuthorizationBuilder().AddPolicy("crm-data-reader", policy => policy
            .RequireAuthenticatedUser().RequireClaim("principal_kind", "service").RequireClaim("sub", clientId)
            .RequireAssertion(context => (context.User.FindFirstValue("scope") ?? "").Split(' ').Contains("crm.read", StringComparer.Ordinal))
            .RequireAssertion(context => Guid.TryParse(context.User.FindFirstValue("tenant_id"), out _)));
        builder.Services.AddKunAuthorization();
        builder.Services.AddSingleton<IPermissionChecker>(new CrmReadPolicy(clientId));
        builder.Services.AddKunEventBus();
        builder.Services.AddQianData<CrmDbContext>((_, options) => options.UseNpgsql(connection).UseKunSchema(CrmDbContext.Schema));
        var app = builder.Build();
        app.UseQianErrors();
        app.UseAuthentication();
        app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"), branch => branch.UseKunMultiTenancy());
        app.UseAuthorization();
        app.MapGet("/api/customers", async (int? skip, int? take, IDataService data, CancellationToken ct) =>
        {
            var offset = skip ?? 0;
            var limit = take ?? 100;
            if (offset < 0 || limit is < 1 or > 100)
                return QianResults.Error(new QianError(PlatformErrors.BadRequest));
            await using var session = await data.OpenAsync<CrmDbContext>(ct);
            var query = session.Context.Customers.AsNoTracking();
            var total = await query.CountAsync(ct);
            var items = await query.TagWith(ListQueryTag).OrderBy(customer => customer.Id).Skip(offset).Take(limit)
                .Select(customer => new CustomerItem(customer.Id, customer.TenantId!, customer.Name)).ToArrayAsync(ct);
            return Results.Ok(new CustomerPage(total, items));
        }).RequireAuthorization("crm-data-reader").RequireKunPermission(CrmReadPolicy.Permission);
        app.MapGet("/api/customers/{id:guid}", async (Guid id, IDataService data, CancellationToken ct) =>
        {
            await using var session = await data.OpenAsync<CrmDbContext>(ct);
            var item = await session.Context.Customers.AsNoTracking().Where(customer => customer.Id == id)
                .Select(customer => new CustomerItem(customer.Id, customer.TenantId!, customer.Name)).SingleOrDefaultAsync(ct);
            return item is null ? QianResults.Error(new QianError(PlatformErrors.NotFound)) : Results.Ok(item);
        }).RequireAuthorization("crm-data-reader").RequireKunPermission(CrmReadPolicy.Permission);
        return app;

        string Required(string key) => builder.Configuration[key] is { Length: > 0 } value
            ? value : throw new InvalidOperationException("缺少必要的 S2S 配置项：" + key);
    }
}
