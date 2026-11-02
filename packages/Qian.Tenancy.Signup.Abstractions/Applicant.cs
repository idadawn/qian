using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Qian.Tenancy.Signup;

/// <summary>请求来源（宿主的 HTTP 层取自连接）；只用于限流与记录，不作授权证据。</summary>
public sealed record SignupRequestContext(string? SourceAddress);

/// <summary>发验证码：手机号接受常见写法，服务端按与身份服务相同的规则规范化为 E.164。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StartSignupVerification([property: MinLength(1), MaxLength(32)] string Phone);

/// <summary>验证码挑战：不论号码是否已注册，形状与时间都相同。</summary>
public sealed record SignupVerificationTicket(Guid Id, DateTimeOffset ExpiresAt, DateTimeOffset ResendAfter);

/// <summary>核验验证码，通过后建立申请人会话。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OpenSignupSession(Guid VerificationId, [property: MinLength(1), MaxLength(16)] string Code)
{
    /// <summary>不输出验证码。</summary>
    public override string ToString() => "OpenSignupSession(redacted)";
}

/// <summary>当前申请人：手机号只给掩码；已有账号时带显示名（核验通过后才告诉申请人「将以既有账号申请」）。</summary>
public sealed record SignupApplicant(string Phone, bool ExistingAccount)
{
    /// <summary>既有账号的显示名；新号码为空。</summary>
    public string? DisplayName { get; init; }
}

/// <summary>新建立的申请人会话；Token 只交给宿主写进 HttpOnly Cookie，不进响应体与日志。</summary>
public sealed record SignupSessionGrant(string Token, DateTimeOffset ExpiresAt, SignupApplicant Applicant)
{
    /// <summary>不输出会话令牌。</summary>
    public override string ToString() => "SignupSessionGrant(redacted)";
}

/// <summary>租户号可用性；不可用时 <see cref="Reason"/> 取 <see cref="SignupCodeReasons"/> 之一。</summary>
public sealed record SignupCodeAvailability(string Code, bool Available)
{
    /// <summary>invalid / reserved / taken；可用时为空。</summary>
    public string? Reason { get; init; }
}

/// <summary>
/// 提交开通申请。新号码必填 <see cref="DisplayName"/> 与 <see cref="Password"/>（密码只交给身份组件哈希保存）；以既有账号申请时两者都不得出现。
/// 目标租户由审核通过时的开通编排生成，请求体不接受任何租户字段。
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubmitSignupApplication(
    [property: MinLength(1), MaxLength(200)] string CompanyName,
    [property: MinLength(1), MaxLength(64), RegularExpression(@"^[a-z0-9]+$(?![\s\S])")] string Code,
    [property: AllowedValues(SignupCompanySizes.Micro, SignupCompanySizes.Small, SignupCompanySizes.Medium, SignupCompanySizes.Large, SignupCompanySizes.Enterprise)] string Size,
    [property: AllowedValues(true)] bool AcceptTerms)
{
    /// <summary>新账号的姓名（即联系人姓名）；既有账号不得填写。</summary>
    [MinLength(1), MaxLength(200)] public string? DisplayName { get; init; }
    /// <summary>新账号的登录密码，按身份组件的密码策略校验；既有账号不得填写。</summary>
    [MinLength(1), MaxLength(1024)] public string? Password { get; init; }
    /// <summary>联系邮箱，只记在申请上，不绑定为登录标识。</summary>
    [MaxLength(254)] public string? Email { get; init; }
    /// <summary>行业。</summary>
    [MaxLength(64)] public string? Industry { get; init; }
    /// <summary>申请说明。</summary>
    [MaxLength(2000)] public string? Description { get; init; }
    /// <summary>不输出密码。</summary>
    public override string ToString() => "SubmitSignupApplication(redacted)";
}

/// <summary>
/// 申请人看到的申请：开通失败（failed）显示为开通中（provisioning），不暴露内部原因；审核备注不出现。手机号为掩码。
/// </summary>
public sealed record SignupApplicantApplication(Guid Id, string Number, SignupApplicationStatus Status, string CompanyName, string Code, string Size,
    string ContactName, string Phone, DateTimeOffset SubmittedAt)
{
    /// <summary>行业。</summary>
    public string? Industry { get; init; }
    /// <summary>申请说明。</summary>
    public string? Description { get; init; }
    /// <summary>联系邮箱。</summary>
    public string? Email { get; init; }
    /// <summary>已通过、已驳回或已撤回的时间。</summary>
    public DateTimeOffset? CompletedAt { get; init; }
    /// <summary>已通过时的开通方式。</summary>
    public SignupPlan? Plan { get; init; }
    /// <summary>已通过时的到期日；正式开通可为空（不过期）。</summary>
    public DateTimeOffset? ValidUntil { get; init; }
    /// <summary>驳回原因（只在已驳回时出现）。</summary>
    public string? RejectReason { get; init; }
}

/// <summary>本人的申请，新的在前。</summary>
public sealed record SignupApplicantApplications(IReadOnlyList<SignupApplicantApplication> Items);

/// <summary>提交成功：新申请与轮换后的申请人会话（会话绑定到申请人的自然人）。</summary>
public sealed record SignupSubmission(SignupApplicantApplication Application, SignupSessionGrant Session);
