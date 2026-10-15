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
        var checker = new RbacPermissionChecker(ctx.Data(), ctx.Services.GetRequiredService<ITenantContext>(),
            ctx.Services.GetRequiredService<IPermissionRegistry>(), ctx.Services.GetService<IAuthzExecutionConstraints>());
        CommandPolicies.Register(ctx, CommandPolicyKind.Authorization, new RbacCommandPolicy(checker).Handle);
        return ValueTask.CompletedTask;
    }
}

// 命令 PEP：按生成能力的 requiredPermission 与 requiredScopes 判定后才委托实际 handler；令牌 roles 不授予权限，S1 的 Agent / act 委托一律拒绝。
internal sealed class RbacCommandPolicy(IPermissionChecker checker)
{
    internal async ValueTask<object?> Handle(CommandInvocation invocation, Func<ValueTask<object?>> next)
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
    }
    private static QianErrorException Error(ErrorCode code) => new(new QianError(code));
}
