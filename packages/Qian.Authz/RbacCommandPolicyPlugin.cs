using System.Text.Json;
using Kun.Authorization;
using Kun.Core.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Authz;

/// <summary>真实 RBAC 命令 PEP；复用活动声明与数据库授权，不代替完整 Authz 管理/数据/字段服务。</summary>
public sealed class RbacCommandPolicyPlugin : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key];
    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx); ct.ThrowIfCancellationRequested();
        if (config.ValueKind != JsonValueKind.Object || config.EnumerateObject().Any()) throw Error(PlatformErrors.InputInvalid);
        var checker = new RbacPermissionChecker(ctx.Data(), ctx.Services.GetRequiredService<ITenantContext>(),
            ctx.Services.GetRequiredService<IPermissionRegistry>(), ctx.Services.GetService<IAuthzExecutionConstraints>());
        CommandPolicies.Register(ctx, CommandPolicyKind.Authorization, async (invocation, next) =>
        {
            var principal = invocation.Principal; var metadata = invocation.Contract.Metadata;
            if (principal.Kind is not (PrincipalKind.User or PrincipalKind.Service) || principal.Actor is not null) throw Error(PlatformErrors.AuthzDenied);
            var scopes = principal.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
            if (metadata.GetProperty("requiredScopes").EnumerateArray().Any(item => !scopes.Contains(item.GetString()!))) throw Error(PlatformErrors.InsufficientScope);
            if (!metadata.TryGetProperty("requiredPermission", out var required) || !PermissionCode.IsValid(required.GetString())) throw Error(PlatformErrors.AuthzDenied);
            var subject = new AuthorizationSubject(principal.Subject, principal.TenantId, principal.Kind == PrincipalKind.User ? "user" : "service");
            var decision = await checker.CheckAsync(new PermissionRequest(subject, required.GetString()!), invocation.CancellationToken).ConfigureAwait(false);
            if (!decision.Allow) throw Error(PlatformErrors.AuthzDenied);
            return await next().ConfigureAwait(false);
        });
        return ValueTask.CompletedTask;
    }
    private static QianErrorException Error(ErrorCode code) => new(new QianError(code));
}
