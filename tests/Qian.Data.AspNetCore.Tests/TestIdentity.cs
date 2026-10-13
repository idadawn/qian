using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Kun.AspNetCore.Authorization;
using Kun.Authorization;
using Kun.MultiTenancy.DependencyInjection;
using Kun.MultiTenancy.Middleware;
using Kun.Security.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Data.AspNetCore.Tests;

/// <summary>
/// 测试宿主的身份边界：请求头 X-Test-User 写「主体」或「主体@租户」，经认证处理器变成已认证身份；租户只取自身份的 tenant_id，
/// 权限由按主体配置的 IPermissionChecker 判定。admin 拥有全部数据权限，reader 只读，nobody 没有任何权限。
/// </summary>
internal static class TestIdentity
{
    public const string Header = "X-Test-User";
    private const string SchemeName = "Test";

    public static void Add(IServiceCollection services)
    {
        services.AddQianErrors();
        services.AddKunSecurity();
        services.AddKunMultiTenancy(options => options.FallbackTenantId = null);
        services.AddAuthentication(SchemeName).AddScheme<AuthenticationSchemeOptions, Handler>(SchemeName, null);
        services.AddKunAuthorization();
        services.AddSingleton<IPermissionChecker, Permissions>();
    }

    public static void Use(WebApplication app)
    {
        app.UseQianErrors();
        app.UseAuthentication();
        app.UseKunMultiTenancy();
        app.UseAuthorization();
    }

    public static HttpRequestMessage Request(HttpMethod method, string uri, string? user, object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        if (user is not null) request.Headers.Add(Header, user);
        if (body is not null) request.Content = new StringContent(body as string ?? JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
        return request;
    }

    private sealed class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(Header, out var value)) return Task.FromResult(AuthenticateResult.NoResult());
            var parts = value.ToString().Split('@');
            List<Claim> claims = [new("sub", parts[0]), new("principal_kind", "user")];
            if (parts.Length > 1) claims.Add(new Claim("tenant_id", parts[1]));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }

    private sealed class Permissions : IPermissionChecker
    {
        private static readonly Dictionary<string, string[]> Grants = new(StringComparer.Ordinal)
        {
            ["admin"] = [DataPermissions.TypeRead, DataPermissions.TypeDelete, DataPermissions.ConnectionRead, DataPermissions.ConnectionWrite, DataPermissions.ConnectionTest],
            ["reader"] = [DataPermissions.TypeRead, DataPermissions.ConnectionRead],
            ["nobody"] = []
        };

        public ValueTask<PermissionDecision> CheckAsync(PermissionRequest request, CancellationToken cancellationToken = default)
        {
            var allow = Grants.TryGetValue(request.Subject.Id, out var granted) && granted.Contains(request.Permission, StringComparer.Ordinal);
            return ValueTask.FromResult(new PermissionDecision(allow, allow ? "granted" : "not-granted", []));
        }

        public async ValueTask<IReadOnlyList<PermissionDecision>> BatchCheckAsync(IReadOnlyList<PermissionRequest> requests, CancellationToken cancellationToken = default)
        {
            var decisions = new List<PermissionDecision>();
            foreach (var request in requests) decisions.Add(await CheckAsync(request, cancellationToken));
            return decisions;
        }

        public ValueTask<IReadOnlyList<string>> ListAllowedActionsAsync(AuthorizationSubject subject, string resourceType, string? resourceId = null,
            CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<string>>([]);

        public ValueTask<IReadOnlyList<string>> ListPermissionsAsync(AuthorizationSubject subject, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<string>>(Grants.GetValueOrDefault(subject.Id) ?? []);

        public ValueTask<PermissionDecision> ExplainAsync(PermissionRequest request, CancellationToken cancellationToken = default) => CheckAsync(request, cancellationToken);
    }
}
