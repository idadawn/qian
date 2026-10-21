using System.Text.Json;
using Kun.Caching.Abstractions;
using Kun.Core.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Authz;

/// <summary>只含命令 PEP 的子插件：复用活动声明与数据库授权；完整 Provider（判定服务、权限登记、开通与超管旁路）见 <see cref="AuthzPlugin"/>。</summary>
public sealed class RbacCommandPolicyPlugin : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key];
    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx); ct.ThrowIfCancellationRequested();
        if (config.ValueKind != JsonValueKind.Object || config.EnumerateObject().Any()) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        var tenant = ctx.Services.GetRequiredService<ITenantContext>();
        var checker = new RbacPermissionChecker(ctx.Data(), tenant, ctx.Services.GetRequiredService<IPermissionRegistry>(),
            ctx.Services.GetService<IAuthzExecutionConstraints>(), ctx.Services.GetService<IKunCache>());
        // 子插件没有平台旁路端口：平台主体一律拒绝；决策审计与完整 Provider 同一规则（经当前可见的 Audit，不可用即拒绝）。
        var enforcement = new AuthorizationEnforcer(ctx, checker, null, tenant, new IdentityRevisionGuard(ctx.Data(), tenant), 1);
        CommandPolicies.Register(ctx, CommandPolicyKind.Authorization, new RbacCommandPolicy(enforcement).Handle);
        return ValueTask.CompletedTask;
    }
}

// 命令 PEP：按生成能力的 requiredPermission 与 requiredScopes 判定后才委托实际 handler；令牌 roles 不授予权限，S1 的 Agent / act 委托一律拒绝。
// 判定、identity_revision 比对与决策审计都经共用执行核心，与 HTTP / gRPC 入口对同一权限点结论一致。
internal sealed class RbacCommandPolicy(IAuthzEnforcement enforcement)
{
    internal async ValueTask<object?> Handle(CommandInvocation invocation, Func<ValueTask<object?>> next)
    {
        if (invocation.Channel == "hook" && TenantInitializationAuthorization.Allows(invocation.Contract.Id, invocation.Source.Scope))
            return await next().ConfigureAwait(false);
        var principal = invocation.Principal; var metadata = invocation.Contract.Metadata;
        if (principal.Kind is not (PrincipalKind.User or PrincipalKind.Service) || principal.Actor is not null) throw Error(PlatformErrors.AuthzDenied);
        var scopes = principal.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        if (metadata.GetProperty("requiredScopes").EnumerateArray().Any(item => !scopes.Contains(item.GetString()!))) throw Error(PlatformErrors.InsufficientScope);
        if (!metadata.TryGetProperty("requiredPermission", out var required) || !PermissionCode.IsValid(required.GetString())) throw Error(PlatformErrors.AuthzDenied);
        var subject = new EnforcementSubject(principal.Subject, principal.TenantId, principal.Kind == PrincipalKind.User ? "user" : "service", principal.IdentityRevision, principal.Actor is not null);
        var decision = await enforcement.EnforceAsync(new EnforcementRequest(subject, required.GetString()!, null, invocation.Channel, AuthzEnforcement.CommandEntryPoint, invocation.Contract.Id,
            invocation.Transaction), invocation.CancellationToken).ConfigureAwait(false);
        if (!decision.Allow) throw Error(decision.Reason == AuthzEnforcement.AuditUnavailable ? PlatformErrors.NotFound : PlatformErrors.AuthzDenied);
        return await next().ConfigureAwait(false);
    }
    private static QianErrorException Error(ErrorCode code) => new(new QianError(code));
}
