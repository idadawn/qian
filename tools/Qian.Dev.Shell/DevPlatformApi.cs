using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.Security.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Authz.AspNetCore;
using Qian.Capabilities.AspNetCore;
using Qian.Dev.TenantOps;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk.AspNetCore;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Dev.Shell;

// 面向用户的平台表面宿主，照参考宿主的 TenantOperationsHost：复用已装配的 Runtime / Data / Authz / Audit / Capabilities，
// 只认网关签发的 S2S（RS256、issuer、受众 qian-platform、有效期），主体快照取自令牌并按其 tenant_id 进入租户 scope。
// 参考应用的命令宿主只认机器身份（service / agent），用户主体的能力导航目录与有效权限因此由这个平台宿主提供——与网关 S2S 的受众分簇一致。
internal static class DevPlatformApi
{
    internal const string Audience = "qian-platform";

    internal static async Task<WebApplication> StartAsync(PluginLoader loader, IServiceProvider business, string issuer, RsaSecurityKey publicKey, CancellationToken ct)
    {
        var tenantContext = business.GetRequiredService<ITenantContext>();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0"); DevHost.Logging(builder.Logging);
        builder.Services.AddSingleton(tenantContext);
        builder.Services.AddSingleton(business.GetRequiredService<ErrorCatalog>());
        builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUser, S2sCurrentUser>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = issuer, ValidateAudience = true, ValidAudience = Audience, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                IssuerSigningKey = publicKey, ValidAlgorithms = [SecurityAlgorithms.RsaSha256], RequireSignedTokens = true, RequireExpirationTime = true, ClockSkew = TimeSpan.Zero
            };
            options.Events.OnTokenValidated = context => { S2sCurrentUser.Capture(context); return Task.CompletedTask; };
        });
        builder.Services.AddAuthorization(); builder.Services.AddQianErrors(); builder.Services.AddQianHttpContracts();
        builder.Services.AddQianPluginEndpoints(_ => loader.GetContext("registry", ScopeKey.Global));
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNameCaseInsensitive = false;
            options.SerializerOptions.RespectRequiredConstructorParameters = true;
            options.SerializerOptions.RespectNullableAnnotations = true;
        });
        var app = builder.Build();
        app.UseQianErrors(); app.UseAuthentication(); app.UseAuthorization(); app.UseQianPluginEndpoints();
        // 租户只取自已验证令牌：成员主体进入其租户 scope；平台身份（tenant_id 为 null）没有租户表面，按未认证拒绝。
        app.Use(async (http, next) =>
        {
            if (http.RequestServices.GetRequiredService<ICurrentUser>().Principal is { TenantId: { } tenant })
            {
                using var scope = tenantContext.BeginScope(tenant);
                await next(http).ConfigureAwait(false);
            }
            else await next(http).ConfigureAwait(false);
        });
        app.MapQianCapabilities(http => Scoped(loader, "capabilities", http));
        app.MapQianAuthz(http => Scoped(loader, "authz", http));
        await app.StartAsync(ct).ConfigureAwait(false);
        return app;
    }

    // 与参考宿主的 CapabilitySource / AuthzSource 同形：全局 Provider 上下文按请求租户收窄。
    private static IPluginContext Scoped(PluginLoader loader, string id, HttpContext http)
    {
        var tenant = http.RequestServices.GetRequiredService<ITenantContext>().TenantId;
        if (!Guid.TryParseExact(tenant, "D", out var parsed) || parsed == Guid.Empty) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        try { return loader.GetContext(id, ScopeKey.Global).Scoped(new ScopeKey("tenant", tenant!)); }
        catch (Exception error) when (error is KeyNotFoundException or ObjectDisposedException or InvalidOperationException)
        { throw new QianErrorException(new QianError(PlatformErrors.NotFound)); }
    }

    // 令牌载荷即主体快照（冻结的 S2S claim 集）；与参考宿主不同，这里接受 user / service / agent 三种主体。
    private sealed class S2sCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
    {
        private static readonly object Snapshot = new();
        public Principal? Principal => accessor.HttpContext?.Items[Snapshot] as Principal;
        public string? UserId => Principal?.Subject;
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true && Principal is not null;
        internal static void Capture(TokenValidatedContext context)
        {
            try
            {
                if (context.SecurityToken is not JsonWebToken token || !PrincipalClaims.TryRead(context.Principal, out var subject, out var tenant, out var kind))
                { context.Fail("缺少已验证的主体。"); return; }
                using var document = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(token.EncodedPayload));
                var payload = document.RootElement;
                if (payload.ValueKind != JsonValueKind.Object || payload.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != payload.EnumerateObject().Count())
                { context.Fail("身份字段重复或无效。"); return; }
                var principal = payload.Deserialize(IdentityJson.PrincipalTypeInfo);
                var principalKind = principal?.Kind switch { PrincipalKind.User => "user", PrincipalKind.Service => "service", PrincipalKind.Agent => "agent", _ => null };
                if (principal is null || principal.Subject != subject || principal.TenantId != tenant || principalKind != kind)
                { context.Fail("主体快照不一致。"); return; }
                context.HttpContext.Items[Snapshot] = principal;
            }
            catch (JsonException) { context.Fail("身份快照无效。"); }
        }
    }
}
