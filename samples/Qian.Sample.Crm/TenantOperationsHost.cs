using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.Security.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Capabilities.AspNetCore;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;
using Qian.Tenancy.AspNetCore;

namespace Qian.Sample.Crm;

// 平台运营的独立真实宿主，复用已装配的 Runtime/Data/Authz/Audit，不经过业务 TenantRequestBoundary。
// 源上下文每次从运行时解析，声明式重挂载后不会持有失效的旧上下文。
public static class TenantOperationsHost
{
    public static WebApplication Build(Func<IPluginContext> source, TokenValidationParameters validation, Action<WebApplicationBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(validation);
        if (!validation.ValidateIssuer || !validation.ValidateAudience || !validation.ValidateLifetime || !validation.ValidateIssuerSigningKey ||
            !validation.RequireSignedTokens || !validation.RequireExpirationTime ||
            validation.ValidAlgorithms is null || !validation.ValidAlgorithms.SequenceEqual([SecurityAlgorithms.RsaSha256]))
            throw new ArgumentException("参考运营宿主要求完整 issuer/audience/lifetime/signature 验证且只接受 RS256。", nameof(validation));
        var services = source().Services;
        var builder = WebApplication.CreateBuilder(); configure?.Invoke(builder);
        builder.Services.AddSingleton(services.GetRequiredService<ITenantContext>());
        builder.Services.AddSingleton(services.GetRequiredService<ErrorCatalog>());
        builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUser, PlatformCurrentUser>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false; options.TokenValidationParameters = validation.Clone();
            options.Events.OnTokenValidated = context => { PlatformCurrentUser.Capture(context); return Task.CompletedTask; };
        });
        builder.Services.AddAuthorization(); builder.Services.AddQianErrors(); builder.Services.AddQianHttpContracts();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNameCaseInsensitive = false;
            options.SerializerOptions.RespectRequiredConstructorParameters = true;
            options.SerializerOptions.RespectNullableAnnotations = true;
        });
        var app = builder.Build(); app.UseQianErrors(); app.UseAuthentication(); app.UseAuthorization();
        app.MapQianTenancy(_ => source());
        return app;
    }

    private sealed class PlatformCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
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
