using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.Security.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Authz.AspNetCore;
using Qian.Capabilities.AspNetCore;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk.AspNetCore;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Sample.Crm;

// 面向用户的平台表面参考宿主（S1-YUE-012）：能力目录 / 导航 / 外壳目录（MapQianCapabilities）与有效权限（MapQianAuthz）。
// 与 TenantOperationsHost 同形：复用已装配的 Runtime（Registry / Capabilities / Authz），只认网关签发的 S2S（RS256、issuer、受众、有效期），
// 接受 user / service / agent 主体，主体快照取自令牌，按其 tenant_id 进入租户 scope；平台身份（tenant_id 为 null）没有租户表面，按未认证拒绝。
// 授权 PEP 按生产模式装配：处理器内判定的端点也比对 identity_revision，旧修订的令牌 401 UNAUTHENTICATED（客户端据此换令牌）。
// 参考应用的命令宿主（受众 qian.sample.crm）验签只认机器身份，用户主体的平台表面因此单独成宿主，网关按受众分簇转发；本宿主借用它的运行时，
// 验证过的主体同时交给参考应用的身份适配器，经通用执行入口调用的能力在应用命令管道里以调用者本人执行（S1-YUE-015）。生产宿主由 platform profile 的平台镜像提供（S1-DEP-009）。
public static class PlatformSurfaceHost
{
    /// <summary>网关为本宿主签发 S2S 时使用的受众（cluster id）。</summary>
    public const string Audience = "qian-platform";

    /// <summary><paramref name="component"/> 按组件 id 返回其全局上下文（每次现取：声明式重挂载后不持有失效的旧上下文）。</summary>
    public static WebApplication Build(Func<string, IPluginContext> component, TokenValidationParameters validation, Action<WebApplicationBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(component); ArgumentNullException.ThrowIfNull(validation);
        if (!validation.ValidateIssuer || !validation.ValidateAudience || !validation.ValidateLifetime || !validation.ValidateIssuerSigningKey ||
            !validation.RequireSignedTokens || !validation.RequireExpirationTime ||
            validation.ValidAlgorithms is null || !validation.ValidAlgorithms.SequenceEqual([SecurityAlgorithms.RsaSha256]))
            throw new ArgumentException("平台表面宿主要求完整 issuer/audience/lifetime/signature 验证且只接受 RS256。", nameof(validation));
        var services = component("registry").Services;
        var tenantContext = services.GetRequiredService<ITenantContext>();
        var builder = WebApplication.CreateBuilder(); configure?.Invoke(builder);
        builder.Services.AddSingleton(tenantContext);
        builder.Services.AddSingleton(services.GetRequiredService<ErrorCatalog>());
        builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUser, S2sCurrentUser>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false; options.TokenValidationParameters = validation.Clone();
            options.Events.OnTokenValidated = context => { S2sCurrentUser.Capture(context); return Task.CompletedTask; };
        });
        builder.Services.AddAuthorization(); builder.Services.AddQianErrors(); builder.Services.AddQianHttpContracts();
        builder.Services.AddQianPluginEndpoints(_ => component("registry"));
        builder.Services.AddQianAuthzEnforcement(http => Scoped(component, "authz", http), options => options.Production = true);
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNameCaseInsensitive = false;
            options.SerializerOptions.RespectRequiredConstructorParameters = true;
            options.SerializerOptions.RespectNullableAnnotations = true;
        });
        var app = builder.Build();
        app.UseQianErrors(); app.UseAuthentication();
        // 租户只取自已验证令牌：成员主体进入其租户 scope（授权判定与修订下限按租户 RLS 读写）；平台身份不进任何租户。
        app.Use(async (http, next) =>
        {
            if (http.RequestServices.GetRequiredService<ICurrentUser>().Principal is { TenantId: { } tenant })
            {
                using var scope = tenantContext.BeginScope(tenant);
                await next(http).ConfigureAwait(false);
            }
            else await next(http).ConfigureAwait(false);
        });
        app.UseAuthorization(); app.UseQianPluginEndpoints(); app.UseQianAuthzEnforcement();
        app.MapQianCapabilities(http => Scoped(component, "capabilities", http));
        app.MapQianAuthz(http => Scoped(component, "authz", http));
        return app;
    }

    // 与参考宿主的 CapabilitySource / AuthzSource 同形：全局 Provider 上下文按请求租户收窄；没有租户（平台身份）即未认证，组件不在即 404。
    private static IPluginContext Scoped(Func<string, IPluginContext> component, string id, HttpContext http)
    {
        var tenant = http.RequestServices.GetRequiredService<ITenantContext>().TenantId;
        if (!Guid.TryParseExact(tenant, "D", out var parsed) || parsed == Guid.Empty) throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
        try { return component(id).Scoped(new ScopeKey("tenant", tenant!)); }
        catch (Exception error) when (error is KeyNotFoundException or ObjectDisposedException or InvalidOperationException)
        { throw new QianErrorException(new QianError(PlatformErrors.NotFound)); }
    }

    // 令牌载荷即主体快照（冻结的 S2S claim 集）；与参考命令宿主不同，这里接受 user / service / agent 三种主体。
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
                // 能力绑定经参考命令宿主的 Mediator 执行、从它的 DI 取当前主体（CrmCurrentUser），所以把同一份已验证快照也交给它（S1-YUE-015 裁决 A1）。
                CrmCurrentUser.AcceptPlatformSurface(context.HttpContext, principal);
            }
            catch (JsonException) { context.Fail("身份快照无效。"); }
        }
    }
}
