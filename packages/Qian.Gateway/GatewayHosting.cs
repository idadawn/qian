using System.Security.Claims;
using System.Text.Json;
using Kun.Gateway;
using Kun.Gateway.DependencyInjection;
using Kun.Gateway.Endpoints;
using Kun.Gateway.Identity;
using Kun.Gateway.RateLimiting;
using Kun.Gateway.RateLimiting.Resolvers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Qian.Gateway;

/// <summary>网关宿主装配；Identity.Client 的 Provider 由宿主独立组合，组件库不直接依赖其他 Provider。</summary>
public static class GatewayHosting
{
    internal const string ExternalScheme = "qian.gateway.external";
    private const string Scheme = "qian.gateway";

    /// <summary>从声明式 JSON 读取严格配置；拒绝重复字段、未知字段与无效静态路由。</summary>
    public static GatewayConfiguration ReadConfiguration(JsonElement configuration) => GatewayConfigurationReader.Read(configuration);

    /// <summary>固定注册认证、路由、可信代理与热轮换。宿主需先装 Identity.Client 的规范 Cookie 方案，并在启用前挂载 GatewayComponent。</summary>
    public static IServiceCollection AddQianGateway(this IServiceCollection services, GatewayConfiguration configuration, string sessionAuthenticationScheme = "qian.session")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionAuthenticationScheme);
        if (services.Any(service => service.ServiceType == typeof(GatewayRuntime))) throw new InvalidOperationException("同一宿主只允许一个网关。");
        var snapshot = GatewayConfigurationReader.Copy(configuration);
        services.AddSingleton(new GatewayRuntime(snapshot));
        services.AddHealthChecks().AddCheck<GatewayMountHealthCheck>("qian.gateway", tags: ["ready"]);
        services.AddQianErrors();
        services.AddSingleton<IS2sPrincipalResolver, GatewayPrincipalResolver>();
        services.AddSingleton<IRateLimitPartitionResolver, IpRateLimitPartitionResolver>();
        services.AddSingleton<IProxyConfigProvider>(new InMemoryConfigProvider(
            snapshot.Routes.Select(route => new RouteConfig
            {
                RouteId = route.Id, ClusterId = route.Cluster, Match = new RouteMatch { Path = route.Path },
                AuthorizationPolicy = "Default", RateLimiterPolicy = "kun-default"
            }).ToArray(),
            snapshot.Clusters.Select(cluster => new ClusterConfig
            {
                ClusterId = cluster.Id, Destinations = new Dictionary<string, DestinationConfig> { ["primary"] = new() { Address = cluster.Address } }
            }).ToArray()));
        services.AddKunGateway(options =>
        {
            options.ExternalIssuer = snapshot.IdentityIssuer; options.ExternalAudience = snapshot.IdentityAudience;
            options.ExternalJwksUri = snapshot.IdentityIssuer.TrimEnd('/') + "/.well-known/openid-configuration";
            options.InternalIssuer = snapshot.Issuer; options.InternalAudience = snapshot.Clusters[0].Id;
            options.InternalTokenLifetime = TimeSpan.FromSeconds(60); options.KeyOverlapPeriod = TimeSpan.FromMinutes(10);
            options.SigningKeyPath = snapshot.SigningKeyPath; options.PublicKeyPath = snapshot.PublicKeyPath;
            options.TrustedProxyAddresses = []; options.TrustedProxyNetworks = snapshot.TrustedProxyNetworks.ToArray(); options.ForwardLimit = 1;
            // tenant_id/sub/roles 是签发器固定字段，不能误填进 Kun 的附加字段兼容选项。
            options.ForwardedClaimTypes = ["principal_kind", "membership_id", "identity_revision", "scope", "session_id", "project_id", "plugin_id"];
        });
        services.AddSingleton<ITransformProvider, AudienceTransformProvider>();
        services.AddAuthentication(options => { options.DefaultScheme = Scheme; options.DefaultChallengeScheme = Scheme; })
            .AddPolicyScheme(Scheme, Scheme, options => options.ForwardDefaultSelector = http =>
                http.Request.Headers.ContainsKey("Authorization") ? ExternalScheme : sessionAuthenticationScheme)
            .AddJwtBearer(ExternalScheme, options =>
            {
                options.MetadataAddress = snapshot.IdentityIssuer.TrimEnd('/') + "/.well-known/openid-configuration";
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = snapshot.IdentityIssuer, ValidateAudience = true, ValidAudience = snapshot.IdentityAudience,
                    ValidateIssuerSigningKey = true, RequireSignedTokens = true, RequireExpirationTime = true, ValidateLifetime = true,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.EcdsaSha256], ValidTypes = ["at+jwt"],
                    AuthenticationType = ExternalScheme, ClockSkew = TimeSpan.FromSeconds(30)
                };
                options.Events.OnTokenValidated = context =>
                {
                    if (context.SecurityToken is not JsonWebToken token) { context.Fail("身份令牌无效。"); return Task.CompletedTask; }
                    using var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(token.EncodedPayload));
                    var root = payload.RootElement;
                    if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != root.EnumerateObject().Count())
                        context.Fail("身份令牌字段重复。");
                    else if (!root.TryGetProperty("sub", out var subject) || subject.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(subject.GetString()))
                        context.Fail("身份令牌缺少主体。");
                    return Task.CompletedTask;
                };
            });
        services.AddOptions<JwtBearerOptions>(ExternalScheme).Configure<IHostEnvironment>((options, environment) =>
            options.RequireHttpsMetadata = !environment.IsDevelopment());
        return services;
    }

    /// <summary>在 UseForwardedHeaders、UseRouting、UseQianIdentityClient 之后调用；业务请求还须通过真实组件挂载门。</summary>
    public static IApplicationBuilder UseQianGateway(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var runtime = app.ApplicationServices.GetRequiredService<GatewayRuntime>();
        app.UseQianErrors();
        app.Use(async (http, next) =>
        {
            if (http.GetEndpoint()?.Metadata.GetMetadata<GatewayEndpoint>() is not null && !runtime.IsReady)
            { http.Response.StatusCode = StatusCodes.Status404NotFound; return; }
            // AuthenticationMiddleware 验签失败不会自动清掉已存在的 Cookie 主体；显式 Authorization 必须独立成功，不能降级回 Cookie。
            if (http.Request.Headers.ContainsKey("Authorization")) http.User = new ClaimsPrincipal(new ClaimsIdentity());
            await next(http).ConfigureAwait(false);
        });
        app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
        app.Use(async (http, next) =>
        {
            if (http.GetEndpoint()?.Metadata.GetMetadata<GatewayEndpoint>() is { } endpoint)
            {
                try
                {
                    http.Items[GatewayRuntime.TokenKey] = await http.RequestServices.GetRequiredService<S2sTokenIssuer>()
                        .IssueForAudienceAsync(http.User, endpoint.Audience, http.RequestAborted).ConfigureAwait(false);
                }
                catch (InvalidOperationException) { throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated)); }
            }
            await next(http).ConfigureAwait(false);
        });
        return app;
    }

    /// <summary>映射受保护静态代理、公开 JWKS 及基础健康端点；完整依赖健康聚合由 GW-003 接续。</summary>
    public static IEndpointRouteBuilder MapQianGateway(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapKunJwks();
        endpoints.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        endpoints.MapHealthChecks("/readyz", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
            ResponseWriter = (http, report) => report.Status == HealthStatus.Healthy
                ? http.Response.WriteAsync("Healthy", http.RequestAborted)
                : QianResults.Error(new QianError(PlatformErrors.Unavailable)).ExecuteAsync(http)
        }).AllowAnonymous();
        var runtime = endpoints.ServiceProvider.GetRequiredService<GatewayRuntime>();
        endpoints.MapReverseProxy().Add(endpoint =>
        {
            var route = endpoint.Metadata.OfType<RouteModel>().Single();
            endpoint.Metadata.Add(new GatewayEndpoint(runtime.AudienceFor(route.Config.ClusterId!)));
        });
        return endpoints;
    }

    private sealed record GatewayEndpoint(string Audience);

    private sealed class AudienceTransformProvider(GatewayRuntime runtime) : ITransformProvider
    {
        public void ValidateRoute(TransformRouteValidationContext context) { }
        public void ValidateCluster(TransformClusterValidationContext context) { }
        public void Apply(TransformBuilderContext context)
        {
            _ = runtime.AudienceFor(context.Route.ClusterId ?? throw new InvalidOperationException("网关路由必须绑定 cluster。"));
            // Kun 注册的固定 audience 变换在前；最后一项明确覆盖为当前 cluster。没有复制或替代其签名、轮换实现。
            context.AddRequestTransform(request =>
            {
                request.ProxyRequest.Headers.Remove("Authorization"); request.ProxyRequest.Headers.Remove("Cookie");
                request.ProxyRequest.Headers.Remove("Forwarded"); request.ProxyRequest.Headers.Remove("X-Forwarded-For");
                request.ProxyRequest.Headers.Remove("X-Forwarded-Proto"); request.ProxyRequest.Headers.Remove("X-Forwarded-Host");
                if (request.HttpContext.Connection.RemoteIpAddress is { } remote)
                    request.ProxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-For", remote.ToString());
                request.ProxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Proto", request.HttpContext.Request.Scheme);
                request.ProxyRequest.Headers.TryAddWithoutValidation("X-Forwarded-Host", new Uri(runtime.Configuration.Issuer).Authority);
                if (!runtime.IsReady || request.HttpContext.User.Identity?.IsAuthenticated != true)
                    throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
                if (request.HttpContext.Items[GatewayRuntime.TokenKey] is not string token)
                    throw new QianErrorException(new QianError(PlatformErrors.Unauthenticated));
                request.ProxyRequest.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
                return ValueTask.CompletedTask;
            });
        }
    }
}

internal sealed class GatewayMountHealthCheck(GatewayRuntime runtime) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(runtime.IsReady ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("网关组件未挂载。"));
}
