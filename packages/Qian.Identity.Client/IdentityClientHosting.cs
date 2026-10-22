using System.Security.Claims;
using System.Text.Json;
using Kun.Identity.Abstractions;
using Kun.Identity.AspNetCore;
using Kun.Identity.Configuration;
using Kun.Identity.DependencyInjection;
using Kun.Security.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Qian.Config;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Identity.Client;

/// <summary>
/// 依赖方装配：服务端授权码 + PKCE 换取、<c>qian.sid</c> Cookie 会话、访问令牌刷新与 discovery 定期刷新，以及下游的 S2S 校验。
/// 薄包装 Kun.Identity / Kun.Identity.AspNetCore / Kun.Security；浏览器流程在 <see cref="IdentityClientPlugin"/> 挂载后才生效。
/// </summary>
public static class IdentityClientHosting
{
    /// <summary>
    /// 登记依赖方服务。配置 <see cref="IdentityClientOptions.RedirectUri"/> 才启用浏览器会话；会话存储默认 Redis（宿主登记 IConnectionMultiplexer），
    /// 多副本须共享 Data Protection 密钥。客户端密钥不经这里传入，只经组件配置的 secretRef 在挂载期解析。
    /// </summary>
    public static IServiceCollection AddQianIdentityClient(this IServiceCollection services, IdentityClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        if (services.Any(descriptor => descriptor.ServiceType == typeof(IdentityClientOptions))) throw new InvalidOperationException("同一宿主只允许一个 Identity Client。");
        services.AddSingleton(options);
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddQianErrors();
        // 回调地址的查询串带授权码与 state，ASP.NET Core 的请求日志会原样打印；与身份服务一致，只保留警告及以上。
        services.AddLogging(logging => logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning));
        // 发行方访问只经这一个命名客户端：不跟随重定向、不带 Cookie，超时 10 秒；宿主可再为它配置代理或受信根证书。
        services.AddHttpClient(IdentityClientDefaults.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });
        services.AddSingleton<IdentityClientRuntime>();
        services.AddSingleton<IdentityAccessTokens>();
        services.AddSingleton(provider => new IdentitySessionTokens(provider.GetRequiredService<IdentityClientRuntime>(), provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<IdentitySessionTokens>>()));
        services.AddHealthChecks().AddCheck<IdentityClientHealthCheck>("qian.identity.client", tags: ["ready"]);
        AddCurrentUser(services);
        if (options.RedirectUri is null) return services;
        // Kun 的登录事务按发行方名称读取 ClientId；密钥不进选项，只在挂载后的 Kun 客户端内部持有。
        services.Configure<KunOidcClientOptions>(IdentityClientDefaults.ProviderName, kun =>
        {
            kun.Name = IdentityClientDefaults.ProviderName; kun.Authority = options.Authority.AbsoluteUri; kun.ClientId = options.ClientId;
            kun.DiscoveryRefreshInterval = options.DiscoveryRefreshInterval; kun.RequireHttpsMetadata = options.RequireHttpsMetadata;
        });
        services.AddSingleton<IOidcClient, ForwardingOidcClient>();
        services.AddKunIdentity(identity => identity.AddCookieSession(session =>
        {
            session.CookieName = IdentityClientDefaults.SessionCookieName; session.SessionTtl = options.SessionLifetime;
            session.Secure = options.RequireSecureCookies ? KunCookieSecurePolicy.Always : KunCookieSecurePolicy.SameAsRequest;
            if (options.UseInMemorySessionStore) session.UseInMemoryStore(); else session.UseRedisStore(options.SessionKeyPrefix);
        }));
        if (options.Resource is not null)
        {
            // 规范模式：缺省方案是 qian.session（Kun 会话之上的访问令牌校验）；先于 Kun 的缺省方案登记。
            services.Configure<AuthenticationOptions>(authentication => authentication.DefaultScheme ??= IdentityClientDefaults.SessionScheme);
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, IdentitySessionHandler>(IdentityClientDefaults.SessionScheme, _ => { });
        }
        services.AddKunIdentityAspNetCore();
        services.Configure<Microsoft.AspNetCore.Antiforgery.AntiforgeryOptions>(antiforgery =>
        {
            antiforgery.HeaderName = IdentityClientDefaults.AntiforgeryHeader;
            antiforgery.Cookie.SecurePolicy = options.RequireSecureCookies ? Microsoft.AspNetCore.Http.CookieSecurePolicy.Always : Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
        });
        return services;
    }

    /// <summary>
    /// 登记下游服务的 S2S 校验（缺省认证方案 Bearer）：只信网关签发器的 issuer 与 JWKS，aud 必须恰好是本服务，载荷按冻结契约逐条校验。
    /// 通过后 <see cref="ICurrentUser.Principal"/> 是令牌的规范快照。可与 <see cref="AddQianIdentityClient"/> 同装，也可单独使用。
    /// </summary>
    public static IServiceCollection AddQianIdentityS2s(this IServiceCollection services, IdentityS2sOptions options)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddQianErrors();
        services.Configure<AuthenticationOptions>(authentication => authentication.DefaultScheme ??= IdentityClientDefaults.S2sScheme);
        services.AddAuthentication().AddKunServiceAuthentication(kun =>
        {
            kun.Issuer = options.Issuer; kun.Audience = options.Audience; kun.JwksUri = options.JwksUri?.AbsoluteUri; kun.StaticKeySet = options.SigningKeys;
        });
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<TimeProvider, ILoggerFactory>((jwt, clock, loggers) =>
            IdentityS2sValidation.Configure(jwt, options, clock, loggers.CreateLogger("Qian.Identity.Client")));
        AddCurrentUser(services);
        return services;
    }

    /// <summary>
    /// 安装依赖方中间件：错误详情、端点门控与禁缓存头、Kun Cookie 会话（含滑动续期与防伪校验），规范模式下再把会话换成已验证的规范主体。
    /// 须在 UseRouting 之后、UseAuthentication 之前调用；组件未挂载时登录端点返回 404，Cookie 会话不生效。
    /// </summary>
    public static IApplicationBuilder UseQianIdentityClient(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var runtime = app.ApplicationServices.GetRequiredService<IdentityClientRuntime>();
        var options = runtime.Options;
        app.UseQianErrors();
        app.Use(async (http, next) =>
        {
            if (http.Request.Path.StartsWithSegments(options.PathPrefix) || options.RedirectUri is { } redirect && http.Request.Path.Equals(redirect.AbsolutePath, StringComparison.Ordinal))
            {
                http.Response.Headers.CacheControl = "no-store"; http.Response.Headers.Pragma = "no-cache";
                http.Response.Headers.XContentTypeOptions = "nosniff"; http.Response.Headers["Referrer-Policy"] = "no-referrer";
                if (!runtime.Active || options.RedirectUri is null) { http.Response.StatusCode = StatusCodes.Status404NotFound; return; }
            }
            await next(http).ConfigureAwait(false);
        });
        if (options.RedirectUri is null) return app;
        return app.UseWhen(_ => runtime.Active, branch =>
        {
            branch.UseKunCookieAuth();
            if (options.Resource is null) return;
            branch.Use(async (http, next) =>
            {
                // 访问令牌校验失败（过期且刷新失败、签名或主体不符）时本请求按匿名处理，不保留 Kun 会话主体。
                if (http.User.Identity is ClaimsIdentity { IsAuthenticated: true } identity && identity.AuthenticationType == KunIdentityAspNetCoreServiceCollectionExtensions.CookieScheme)
                {
                    var result = await http.AuthenticateAsync(IdentityClientDefaults.SessionScheme).ConfigureAwait(false);
                    http.User = result is { Succeeded: true, Principal: { } principal } ? principal : new ClaimsPrincipal(new ClaimsIdentity());
                }
                await next(http).ConfigureAwait(false);
            });
        });
    }

    /// <summary>映射登录、回调（RedirectUri 的路径）、登出、防伪令牌与 OIDC 后通道登出端点；未配置 RedirectUri 时不映射。</summary>
    public static IEndpointRouteBuilder MapQianIdentityClient(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var options = endpoints.ServiceProvider.GetRequiredService<IdentityClientOptions>();
        if (options.RedirectUri is null) return endpoints;
        endpoints.MapGet(options.LoginPath, (RequestDelegate)IdentityClientEndpoints.LoginAsync).AllowAnonymous();
        endpoints.MapGet(options.RedirectUri.AbsolutePath, (RequestDelegate)IdentityClientEndpoints.CallbackAsync).AllowAnonymous();
        endpoints.MapPost(options.LogoutPath, (RequestDelegate)IdentityClientEndpoints.LogoutAsync).AllowAnonymous();
        endpoints.MapGet(options.CsrfPath, (RequestDelegate)IdentityClientEndpoints.CsrfAsync).AllowAnonymous();
        endpoints.MapKunBackChannelLogout(options.BackchannelPath, IdentityClientDefaults.ProviderName);
        return endpoints;
    }

    // Qian 与 Kun 的当前用户端口都落到同一个实现；先于 Kun.Identity.AspNetCore 的 TryAdd 登记。
    private static void AddCurrentUser(IServiceCollection services)
    {
        services.TryAddScoped<ICurrentUser, IdentityClientCurrentUser>();
        services.TryAddScoped<Kun.Core.Security.ICurrentUser>(provider => provider.GetRequiredService<ICurrentUser>());
    }
}

/// <summary>
/// Identity seam 的依赖方 Provider（组件 <c>qian.identity.client</c>，只允许 global scope）。配置的 issuer、clientId 必须与宿主
/// <see cref="IdentityClientOptions"/> 一致；clientSecret 只接受 platform / dev 的 secretRef，第一次换码时经 Config 的密钥策略解析，
/// 公开客户端省略它即可不依赖 Config。卸载后浏览器端点返回 404，Cookie 会话不再生效。
/// </summary>
public sealed class IdentityClientPlugin : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [];

    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var runtime = ctx.Services.GetRequiredService<IdentityClientRuntime>();
        var options = runtime.Options;
        if (ctx.Scope != ScopeKey.Global || config.ValueKind != JsonValueKind.Object) throw Invalid();
        string? issuer = null, clientId = null, secret = null;
        foreach (var property in config.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String) throw Invalid();
            switch (property.Name)
            {
                case "issuer": issuer = property.Value.GetString(); break;
                case "clientId": clientId = property.Value.GetString(); break;
                case "clientSecret": secret = property.Value.GetString(); break;
                default: throw Invalid();
            }
        }
        if (issuer?.TrimEnd('/') != options.Issuer || clientId != options.ClientId) throw Invalid();
        if (secret is not null)
        {
            try { if (SecretReference.Parse(secret).Scope is not ("platform" or "dev")) throw Invalid(); }
            catch (FormatException) { throw Invalid(); }
        }
        ctx.Effect(IdentityErrors.Register(ctx.Services.GetRequiredService<ErrorCatalog>()));
        runtime.Activate(ctx, secret);
        ctx.Effect(runtime.Deactivate);
        ctx.Provide<IIdentityService>(IdentityDefinition.Key, new IdentityClientService(ctx.Services.GetRequiredService<IHttpContextAccessor>(), ctx.Services));
        return ValueTask.CompletedTask;
    }

    private static QianErrorException Invalid() => new(new QianError(PlatformErrors.InputInvalid));
}
