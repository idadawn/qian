namespace Qian.Tenancy.Signup;

/// <summary>
/// 开通申请服务（规格 §3–§7）。申请人一侧以申请人会话令牌（宿主写在 HttpOnly Cookie 里的原值，服务端只存 SHA-256）识别本人，
/// 只能读写本人的申请；运营一侧调用前必须已完成平台运营授权（平台主体 + 超管旁路 + 平台审计链，R113 同序），拒绝租户上下文。
/// 账号只经身份组件的端口建立与读取，开通只经 qian.tenancy 的开通编排与状态机。
/// </summary>
public interface ITenantSignup
{
    /// <summary>发验证码；号码已注册与否结果相同。超限抛 RATE_LIMITED（Data 带 <see cref="TenancySignupErrors.RetryAfterDataKey"/>），没有发件端口抛 SIGNUP_UNAVAILABLE。</summary>
    ValueTask<SignupVerificationTicket> StartVerificationAsync(StartSignupVerification request, SignupRequestContext context, CancellationToken cancellationToken = default);
    /// <summary>核验验证码并建立申请人会话；验证码单次有效，错误达到上限即作废。号码的账号停用、锁定或是平台身份时抛 SIGNUP_ACCOUNT_UNAVAILABLE。</summary>
    ValueTask<SignupSessionGrant> OpenSessionAsync(OpenSignupSession request, SignupRequestContext context, CancellationToken cancellationToken = default);
    /// <summary>读取当前申请人；会话无效返回 null。</summary>
    ValueTask<SignupApplicant?> GetApplicantAsync(string sessionToken, CancellationToken cancellationToken = default);
    /// <summary>吊销申请人会话；幂等。</summary>
    ValueTask CloseSessionAsync(string sessionToken, CancellationToken cancellationToken = default);
    /// <summary>租户号可用性；按来源地址限流。</summary>
    ValueTask<SignupCodeAvailability> CheckCodeAsync(string code, SignupRequestContext context, CancellationToken cancellationToken = default);
    /// <summary>本人的申请，新的在前；会话无效抛 UNAUTHENTICATED。</summary>
    ValueTask<SignupApplicantApplications> ListMineAsync(string sessionToken, CancellationToken cancellationToken = default);
    /// <summary>提交申请；新号码在此建立自然人。成功后轮换申请人会话。</summary>
    ValueTask<SignupSubmission> SubmitAsync(string sessionToken, SubmitSignupApplication request, CancellationToken cancellationToken = default);
    /// <summary>撤回本人待审核的申请；不是本人的申请返回 NOT_FOUND，其它状态 SIGNUP_STATE_CONFLICT。</summary>
    ValueTask<SignupApplicantApplication> WithdrawAsync(string sessionToken, Guid id, CancellationToken cancellationToken = default);

    /// <summary>运营列表、总数与各状态计数；每行带一条最需要关注的自动检查。</summary>
    ValueTask<SignupApplicationList> QueryAsync(SignupApplicationQuery query, CancellationToken cancellationToken = default);
    /// <summary>运营详情：企业信息、申请人、自动检查、审核结果、开通步骤与缺省值。</summary>
    ValueTask<SignupApplicationDetail> GetAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>通过并开通：申请转 provisioning，经开通编排建立租户与管理员成员后激活为试用或正式；编排失败时申请转 failed 并照常返回。</summary>
    ValueTask<SignupApplicationDetail> ApproveAsync(Guid id, ApproveSignupApplication request, string actor, CancellationToken cancellationToken = default);
    /// <summary>驳回：原因对申请人可见，租户号释放。</summary>
    ValueTask<SignupApplicationDetail> RejectAsync(Guid id, RejectSignupApplication request, string actor, CancellationToken cancellationToken = default);
    /// <summary>对开通失败（或执行者已不在的开通中）的申请续跑开通与激活；同一输入幂等，不重复建租户或成员。</summary>
    ValueTask<SignupApplicationDetail> RetryAsync(Guid id, string actor, CancellationToken cancellationToken = default);
}
