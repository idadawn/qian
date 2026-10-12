using Kun.Authorization;

namespace Qian.Authz.Abstractions;

/// <summary>由可信身份/终端适配器校验 step-up 与终端条件；不可从请求体的布尔值直接构造放行。</summary>
public interface IAuthzExecutionConstraints
{
    /// <summary>校验当前请求的附加执行条件；没有足够身份或终端证据时拒绝。</summary>
    ValueTask<PermissionDecision> CheckAsync(AuthorizationSubject subject, PermissionDefinition permission, CancellationToken cancellationToken = default);
}
