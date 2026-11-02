namespace Qian.Tenancy.Signup;

/// <summary>
/// 发件端口（规格 §7）：验证码、开通结果、驳回结果。通知组件（短信渠道）就绪前生产宿主不提供实现；
/// 开发宿主登记开发期发件箱，只在 Development 环境、只监听回环地址。实现须按消息 Id 去重。
/// </summary>
public interface ISignupMessenger
{
    /// <summary>发送验证码；失败时发码请求返回 503 SIGNUP_UNAVAILABLE，该验证码作废。</summary>
    ValueTask SendVerificationCodeAsync(SignupVerificationMessage message, CancellationToken cancellationToken = default);
    /// <summary>发送审核结果；失败只记录，不阻断审核，申请人仍可在进度页查看。</summary>
    ValueTask SendResultAsync(SignupResultMessage message, CancellationToken cancellationToken = default);
}

/// <summary>验证码消息；Id 即验证码挑战 ID。</summary>
public sealed record SignupVerificationMessage(Guid Id, string Phone, string Code, DateTimeOffset ExpiresAt)
{
    /// <summary>不输出手机号与验证码。</summary>
    public override string ToString() => "SignupVerificationMessage(redacted)";
}

/// <summary>审核结果的种类。</summary>
public enum SignupResultKind
{
    /// <summary>已开通。</summary>
    Approved,
    /// <summary>已驳回。</summary>
    Rejected
}

/// <summary>审核结果消息；Id 即申请 ID，同一申请同一结果只发一次。</summary>
public sealed record SignupResultMessage(Guid Id, string Phone, string ApplicationNumber, SignupResultKind Kind)
{
    /// <summary>开通的租户号（已开通时）。</summary>
    public string? TenantCode { get; init; }
    /// <summary>到期日（已开通且有到期日时）。</summary>
    public DateTimeOffset? ValidUntil { get; init; }
    /// <summary>驳回原因（已驳回时）。</summary>
    public string? Reason { get; init; }
    /// <summary>不输出手机号。</summary>
    public override string ToString() => "SignupResultMessage(redacted)";
}
