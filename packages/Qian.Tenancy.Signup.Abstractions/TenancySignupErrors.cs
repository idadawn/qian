using Qian.Runtime.Errors;

namespace Qian.Tenancy.Signup;

/// <summary>开通申请的稳定错误码，归属 qian.tenancy.signup；协议级拒绝复用冻结内置码（RATE_LIMITED、INPUT_INVALID 等）。</summary>
/// <remarks>发验证码的响应不区分号码是否已注册；核验通过后才告诉申请人「将以既有账号申请」。</remarks>
public static class TenancySignupErrors
{
    private const string Owner = TenancySignupDefinition.ComponentId;

    /// <summary>
    /// RATE_LIMITED 的 <see cref="QianErrorException"/> 在 <see cref="Exception.Data"/> 里以此键携带建议等待的整秒数（int），宿主据此写 Retry-After 头。
    /// </summary>
    public const string RetryAfterDataKey = "qian.signup.retryAfter";

    /// <summary>没有可用的发件端口（通知组件未就绪），自助开通暂不可用。</summary>
    public static readonly ErrorCode Unavailable = ErrorCode.Protocol("SIGNUP_UNAVAILABLE", 503, Owner,
        Text("自助开通暂不可用", "Self-service signup unavailable"),
        Text("验证码暂时无法发送，自助开通暂不可用，请稍后重试或联系平台运营。", "Verification codes cannot be sent right now, so self-service signup is unavailable; retry later or contact platform operations."),
        retryable: true);
    /// <summary>验证码不正确；remaining 为本次验证码还能尝试的次数，为 0 时该验证码已作废。</summary>
    public static readonly ErrorCode CodeInvalid = ErrorCode.Protocol("SIGNUP_CODE_INVALID", 400, Owner,
        Text("验证码不正确", "Incorrect verification code"),
        Text("验证码不正确，还可以再试 {remaining} 次。", "The verification code is incorrect; {remaining} attempt(s) left."));
    /// <summary>验证码已过期、已使用、已作废或不存在，须重新获取。</summary>
    public static readonly ErrorCode CodeExpired = ErrorCode.Protocol("SIGNUP_CODE_EXPIRED", 400, Owner,
        Text("验证码已失效", "Verification code expired"),
        Text("验证码已过期或已失效，请重新获取。", "The verification code has expired or is no longer valid; request a new one."));
    /// <summary>该号码对应的账号已停用、锁定，或是平台身份（Q12：平台身份不进租户），不能申请开通。</summary>
    public static readonly ErrorCode AccountUnavailable = ErrorCode.Protocol("SIGNUP_ACCOUNT_UNAVAILABLE", 403, Owner,
        Text("账号不可申请", "Account unavailable"),
        Text("该手机号对应的账号当前不能申请开通企业，请联系平台运营。", "The account bound to this phone number cannot apply for a new organization; contact platform operations."));
    /// <summary>该账号已有一条待审核、开通中或开通失败的申请。</summary>
    public static readonly ErrorCode ApplicationOpen = ErrorCode.Business("SIGNUP_APPLICATION_OPEN", 409, Owner,
        Text("已有进行中的申请", "Application in progress"),
        Text("你已有一条进行中的开通申请，请等待审核结果或先撤回。", "You already have an application in progress; wait for the review or withdraw it first."));
    /// <summary>租户号已被已有租户或其它进行中的申请占用。</summary>
    public static readonly ErrorCode CodeTaken = ErrorCode.Business("SIGNUP_CODE_TAKEN", 409, Owner,
        Text("租户号已被占用", "Tenant code taken"),
        Text("该租户号已被占用，请换一个。", "The tenant code is already taken; choose another one."));
    /// <summary>申请当前状态不允许该操作（例如撤回已进入审核的申请、重复审核）。</summary>
    public static readonly ErrorCode StateConflict = ErrorCode.Business("SIGNUP_STATE_CONFLICT", 409, Owner,
        Text("申请状态冲突", "Application state conflict"),
        Text("开通申请当前的状态不允许该操作，请刷新后重试。", "The application's current state does not allow this operation; refresh and retry."));

    /// <summary>本组件定义的全部错误码；组件挂载时逐个登记到宿主的错误码目录。</summary>
    public static IReadOnlyList<ErrorCode> All { get; } = [Unavailable, CodeInvalid, CodeExpired, AccountUnavailable, ApplicationOpen, CodeTaken, StateConflict];

    private static Dictionary<string, string> Text(string zh, string en) => new() { ["zh-CN"] = zh, ["en"] = en };
}
