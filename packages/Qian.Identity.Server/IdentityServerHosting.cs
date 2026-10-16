using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenIddict.Server;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;
using System.Text.Json;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Qian.Identity.Server;

/// <summary>ASP.NET Core 宿主的协议装配；Data 上下文与账号 Cookie scheme 由宿主组合。</summary>
public static class IdentityServerHosting
{
    /// <summary>配置协议库；不会自行迁移数据库、创建账号或挂载组件。</summary>
    public static IServiceCollection AddQianIdentityServer(this IServiceCollection services, IdentityServerOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(options); ArgumentNullException.ThrowIfNull(environment);
        Validate(options, environment);
        services.AddSingleton(options); services.AddSingleton<IdentityProtocolGate>();
        services.AddHostedService<IdentityBackchannelWorker>();
        services.AddHttpClient("Qian.Identity.Backchannel", client => client.Timeout = TimeSpan.FromSeconds(5))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        services.AddAntiforgery(); services.AddQianErrors();
        // 协议库的 Trace 会打印令牌，Hosting 的请求日志会打印 id_token_hint 查询串；保留警告及以上诊断。
        services.AddLogging(logging => logging.AddFilter("OpenIddict", LogLevel.Warning)
            .AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning));
        services.AddOpenIddict().AddCore(core => core.DisableEntityCaching().UseEntityFrameworkCore().UseDbContext<IdentityDbContext>())
            .AddServer(server =>
            {
                server.SetIssuer(options.Issuer).SetAuthorizationEndpointUris("/connect/authorize").SetTokenEndpointUris("/connect/token")
                    .SetUserInfoEndpointUris("/connect/userinfo").SetRevocationEndpointUris("/connect/revocation")
                    .SetIntrospectionEndpointUris("/connect/introspection").SetEndSessionEndpointUris("/connect/logout");
                server.AllowAuthorizationCodeFlow().AllowRefreshTokenFlow().AllowClientCredentialsFlow().RequireProofKeyForCodeExchange();
                server.Configure(configuration => configuration.CodeChallengeMethods.Remove(CodeChallengeMethods.Plain));
                server.SetAccessTokenLifetime(TimeSpan.FromMinutes(15)).SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(5))
                    .SetRefreshTokenLifetime(TimeSpan.FromDays(14)).SetRefreshTokenReuseLeeway(TimeSpan.Zero);
                server.RegisterScopes(options.Clients.SelectMany(client => client.Application.Permissions)
                    .Where(permission => permission.StartsWith(Permissions.Prefixes.Scope, StringComparison.Ordinal))
                    .Select(permission => permission[Permissions.Prefixes.Scope.Length..]).Distinct(StringComparer.Ordinal).ToArray());
                foreach (var certificate in options.SigningCertificates) server.AddSigningCertificate(certificate);
                foreach (var certificate in options.EncryptionCertificates) server.AddEncryptionCertificate(certificate);
                // access token 可由 RP 使用 JWKS 验签；授权码与 refresh token 继续强制加密。
                server.DisableAccessTokenEncryption();
                server.AddEventHandler<OpenIddictServerEvents.GenerateTokenContext>(handler => handler
                    .SetOrder(OpenIddictServerHandlers.Protection.AttachTokenMetadata.Descriptor.Order + 100)
                    .UseInlineHandler(context =>
                    {
                        // ClaimsIdentity 的 JSON_NULL 经 IdentityModel Subject 序列化会退化为空字符串；显式写入 payload 的 JSON null。
                        if (context.TokenType == TokenTypeIdentifiers.AccessToken && context.Principal.HasClaim(claim => claim.Type == "tenant_id" && claim.ValueType == JsonClaimValueTypes.JsonNull))
                        {
                            var subject = context.SecurityTokenDescriptor.Subject!;
                            foreach (var claim in subject.FindAll("tenant_id").ToArray()) subject.RemoveClaim(claim);
                            context.SecurityTokenDescriptor.Claims!["tenant_id"] = null!;
                        }
                        return ValueTask.CompletedTask;
                    }));
                var aspnet = server.UseAspNetCore().EnableAuthorizationEndpointPassthrough().EnableTokenEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough().EnableEndSessionEndpointPassthrough();
                if (options.AllowLoopbackHttp) aspnet.DisableTransportSecurityRequirement();
            });
        return services;
    }

    /// <summary>必须在 UseAuthentication 前安装；卸载组件后协议面返回 404。</summary>
    public static IApplicationBuilder UseQianIdentityServer(this IApplicationBuilder app)
    {
        app.UseQianErrors();
        return app.Use(async (http, next) =>
        {
            if (http.Request.Path.StartsWithSegments("/connect") || http.Request.Path.StartsWithSegments("/.well-known"))
            {
                http.Response.Headers.CacheControl = "no-store"; http.Response.Headers.Pragma = "no-cache";
                http.Response.Headers.XContentTypeOptions = "nosniff"; http.Response.Headers.ContentSecurityPolicy = "default-src 'none'; form-action 'self'; frame-ancestors 'none'";
                http.Response.Headers["Referrer-Policy"] = "no-referrer";
                if (!http.RequestServices.GetRequiredService<IdentityProtocolGate>().Active) { http.Response.StatusCode = 404; return; }
            }
            await next(http).ConfigureAwait(false);
        });
    }

    private static void Validate(IdentityServerOptions options, IHostEnvironment environment)
    {
        var insecure = options.AllowLoopbackHttp && environment.IsDevelopment() && options.Issuer.IsAbsoluteUri && options.Issuer.IsLoopback;
        if (!options.Issuer.IsAbsoluteUri || options.Issuer.UserInfo.Length != 0 || options.Issuer.Query.Length != 0 || options.Issuer.Fragment.Length != 0 ||
            (options.Issuer.Scheme != "https" && !(insecure && options.Issuer.Scheme == "http")) || options.AllowLoopbackHttp && !insecure ||
            string.IsNullOrWhiteSpace(options.SignInScheme) || options.SigningCertificates.Count == 0 || options.EncryptionCertificates.Count == 0 ||
            options.SigningCertificates.Concat(options.EncryptionCertificates).Any(certificate => !certificate.HasPrivateKey || certificate.PublicKey.Oid.Value != "1.2.840.113549.1.1.1") ||
            options.Clients.Select(client => client.Application.ClientId).Distinct(StringComparer.Ordinal).Count() != options.Clients.Count)
            throw new ArgumentException("身份服务的 issuer、密钥或客户端配置无效。", nameof(options));
        foreach (var client in options.Clients)
        {
            var descriptor = client.Application;
            if (string.IsNullOrWhiteSpace(descriptor.ClientId) || descriptor.ClientType is not (ClientTypes.Public or ClientTypes.Confidential) || descriptor.ConsentType != ConsentTypes.Implicit ||
                descriptor.ClientType == ClientTypes.Confidential && string.IsNullOrWhiteSpace(descriptor.ClientSecret) ||
                descriptor.Permissions.Contains(Permissions.GrantTypes.ClientCredentials) && descriptor.ClientType != ClientTypes.Confidential ||
                client.TenantId == Guid.Empty || client.Resources.Count == 0 || client.Resources.Any(resource => !Uri.TryCreate(resource, UriKind.Absolute, out var uri) || uri.Fragment.Length != 0) ||
                client.BackchannelLogoutUri is { } callback && (!callback.IsAbsoluteUri || callback.UserInfo.Length != 0 || callback.Fragment.Length != 0 || callback.Query.Length != 0 ||
                    callback.Scheme != "https" && !(insecure && callback.IsLoopback && callback.Scheme == "http")))
                throw new ArgumentException("客户端必须具有受信类型、精确受众及安全后通道地址。", nameof(options));
        }
    }
}

/// <summary>Identity Provider 的可撤销挂载；账号服务可由独立宿主提供，平台账号实现由 IDP-003 承接。</summary>
public sealed class IdentityServerPlugin(IIdentityService accounts) : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [Qian.Data.DataDefinition.Key];
    /// <inheritdoc />
    public async ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        var options = ctx.Services.GetRequiredService<IdentityServerOptions>();
        if (ctx.Scope != ScopeKey.Global || config.ValueKind != JsonValueKind.Object || config.EnumerateObject().Count() != 1 ||
            !config.TryGetProperty("issuer", out var issuer) || issuer.ValueKind != JsonValueKind.String || issuer.GetString() != options.Issuer.AbsoluteUri)
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        await using var scope = ctx.Services.CreateAsyncScope();
        var gate = ctx.Services.GetRequiredService<IdentityProtocolGate>();
        if (gate.Active) throw new InvalidOperationException("同一宿主只允许一个 Identity Server。");
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await using var transaction = await database.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtextextended('qian.identity:clients', 0))", ct).ConfigureAwait(false);
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        foreach (var client in options.Clients)
        {
            var existing = await applications.FindByClientIdAsync(client.Application.ClientId!, ct).ConfigureAwait(false);
            if (existing is null) await applications.CreateAsync(client.Application, ct).ConfigureAwait(false);
            else await applications.UpdateAsync(existing, client.Application, ct).ConfigureAwait(false);
        }
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        ctx.Provide(IdentityDefinition.Key, accounts);
        gate.Active = true; ctx.Effect(() => { gate.Active = false; });
    }
}
internal sealed class IdentityProtocolGate
{
    private int _active;
    internal bool Active { get => Volatile.Read(ref _active) == 1; set => Volatile.Write(ref _active, value ? 1 : 0); }
}
