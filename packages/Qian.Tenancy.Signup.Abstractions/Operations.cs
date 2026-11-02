using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Qian.Tenancy.Signup;

/// <summary>运营列表的筛选与分页；关键字匹配申请编号、企业名称、租户号与联系人姓名。</summary>
public sealed record SignupApplicationQuery(int Skip = 0, int Take = 50)
{
    /// <summary>按状态筛选；为空表示全部。</summary>
    public SignupApplicationStatus? Status { get; init; }
    /// <summary>关键字。</summary>
    public string? Keyword { get; init; }
    /// <summary>按企业规模筛选（<see cref="SignupCompanySizes"/>）。</summary>
    public string? Size { get; init; }
}

/// <summary>运营列表：当前页、筛选后的总数与各状态计数（计数只受关键字与规模筛选影响，不受状态筛选影响）。</summary>
public sealed record SignupApplicationList(IReadOnlyList<SignupApplicationSummary> Items, int Total, SignupApplicationCounts Counts);

/// <summary>各状态的申请数与全部。</summary>
public sealed record SignupApplicationCounts(int All, int Submitted, int Provisioning, int Approved, int Failed, int Rejected, int Withdrawn);

/// <summary>一条自动检查：种类见 <see cref="SignupCheckKinds"/>，级别见 <see cref="SignupCheckLevels"/>。</summary>
public sealed record SignupCheck(string Kind, string Level)
{
    /// <summary>租户号检查不通过时的原因（<see cref="SignupCodeReasons"/>）。</summary>
    public string? Reason { get; init; }
    /// <summary>命中的数量：同名的租户与申请数、申请人已加入的企业数。</summary>
    public int? Count { get; init; }
}

/// <summary>
/// 运营列表的一行；手机号为掩码。<see cref="Check"/> 是最需要关注的一条自动检查，只对进行中的申请（待审核、开通中、开通失败）计算，
/// 已通过、已驳回、已撤回的为空。
/// </summary>
public sealed record SignupApplicationSummary(Guid Id, string Number, SignupApplicationStatus Status, string CompanyName, string Code, string Size,
    string ContactName, string Phone, DateTimeOffset SubmittedAt, SignupCheck? Check)
{
    /// <summary>行业。</summary>
    public string? Industry { get; init; }
}

/// <summary>企业信息。</summary>
public sealed record SignupCompany(string Name, string Code, string Size)
{
    /// <summary>行业。</summary>
    public string? Industry { get; init; }
    /// <summary>申请说明。</summary>
    public string? Description { get; init; }
}

/// <summary>申请人：SubjectId 即自然人 ID（令牌的 sub）；手机号为掩码。</summary>
public sealed record SignupApplicantProfile(Guid SubjectId, string ContactName, string Phone, bool ExistingAccount)
{
    /// <summary>联系邮箱。</summary>
    public string? Email { get; init; }
}

/// <summary>审核抽屉的缺省值（来自组件配置）。</summary>
public sealed record SignupReviewDefaults(int TrialDays, int Users);

/// <summary>审核结果；审核备注只给运营看。</summary>
public sealed record SignupReview(string Reviewer, DateTimeOffset ReviewedAt)
{
    /// <summary>通过时的开通方式。</summary>
    public SignupPlan? Plan { get; init; }
    /// <summary>通过时的到期日。</summary>
    public DateTimeOffset? ValidUntil { get; init; }
    /// <summary>通过时的用户配额（含租户管理员，不含平台超管）。</summary>
    public int? Users { get; init; }
    /// <summary>驳回原因，对申请人可见。</summary>
    public string? RejectReason { get; init; }
    /// <summary>审核备注，只给运营看。</summary>
    public string? Note { get; init; }
}

/// <summary>开通编排的一步（取自 qian.tenancy 的步骤表）；Reason 只是稳定原因码。</summary>
public sealed record SignupProvisionStep(string Step, string Status, int Attempts)
{
    /// <summary>失败或跳过的原因码。</summary>
    public string? Reason { get; init; }
}

/// <summary>开通进度：租户与管理员成员在申请上只生成一次，重试沿用。</summary>
public sealed record SignupProvisioning(int Attempts, IReadOnlyList<SignupProvisionStep> Steps)
{
    /// <summary>开通编排建立的租户 ID。</summary>
    public Guid? TenantId { get; init; }
    /// <summary>申请人在新租户的管理员成员 ID。</summary>
    public Guid? MembershipId { get; init; }
    /// <summary>最近一次开通失败的原因码。</summary>
    public string? FailureReason { get; init; }
}

/// <summary>运营详情：企业信息、申请人（掩码）、自动检查（只对进行中的申请计算，终态为空集合）、审核结果、开通步骤与审核缺省值。</summary>
public sealed record SignupApplicationDetail(Guid Id, string Number, SignupApplicationStatus Status, SignupCompany Company, SignupApplicantProfile Applicant,
    IReadOnlyList<SignupCheck> Checks, SignupReviewDefaults Defaults, DateTimeOffset SubmittedAt)
{
    /// <summary>已通过、已驳回或已撤回的时间。</summary>
    public DateTimeOffset? CompletedAt { get; init; }
    /// <summary>审核结果；待审核与已撤回时为空。</summary>
    public SignupReview? Review { get; init; }
    /// <summary>开通进度；尚未通过时为空。</summary>
    public SignupProvisioning? Provisioning { get; init; }
}

/// <summary>审核通过：租户号可在通过前修正；缺省值见 <see cref="SignupReviewDefaults"/>。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ApproveSignupApplication
{
    /// <summary>开通方式。</summary>
    [JsonRequired] public SignupPlan Plan { get; init; }
    /// <summary>修正后的租户号；为空沿用申请里的。</summary>
    [MinLength(1), MaxLength(64), RegularExpression(@"^[a-z0-9]+$(?![\s\S])")] public string? Code { get; init; }
    /// <summary>到期日；试用缺省为当日加试用天数，正式缺省不过期。须晚于当前时间。</summary>
    public DateTimeOffset? ValidUntil { get; init; }
    /// <summary>用户配额；缺省取组件配置。</summary>
    [Range(1, 100000)] public int? Users { get; init; }
    /// <summary>审核备注，只给运营看。</summary>
    [MaxLength(2000)] public string? Note { get; init; }
}

/// <summary>驳回：原因必填且对申请人可见，备注只给运营看。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RejectSignupApplication([property: MinLength(1), MaxLength(500)] string Reason)
{
    /// <summary>审核备注，只给运营看。</summary>
    [MaxLength(2000)] public string? Note { get; init; }
}

/// <summary>申请状态机的一条迁移。</summary>
public sealed record SignupTransition(IReadOnlyList<SignupApplicationStatus> From, string Action, SignupApplicationStatus To);

/// <summary>Agent 可见性：humanOnly 读作 expose=false（R52），requiresConfirmation 要求界面二次确认。</summary>
public sealed record SignupActionAgent(bool Expose, bool RequiresConfirmation);

/// <summary>一个操作的机器可读描述。</summary>
public sealed record SignupAction(string Name, string Actor, string SideEffect, SignupActionAgent Agent);

/// <summary>开通申请状态机与操作风险的机器可读描述（规格 §3、§5.2）。</summary>
public sealed record SignupStateMachine(IReadOnlyList<SignupApplicationStatus> States, IReadOnlyList<SignupApplicationStatus> Terminal,
    IReadOnlyList<SignupTransition> Transitions, IReadOnlyList<SignupAction> Actions)
{
    /// <summary>
    /// 当前版本的描述：通过、驳回只给人（expose=false）且要求确认；重试与申请人的撤回不对 Agent 暴露。
    /// 重试除 failed 外也接受执行者已不在的 provisioning（进程中断留下的），以免申请停在开通中。
    /// </summary>
    public static SignupStateMachine Current { get; } = new(
        [SignupApplicationStatus.Submitted, SignupApplicationStatus.Provisioning, SignupApplicationStatus.Approved, SignupApplicationStatus.Failed,
            SignupApplicationStatus.Rejected, SignupApplicationStatus.Withdrawn],
        [SignupApplicationStatus.Approved, SignupApplicationStatus.Rejected, SignupApplicationStatus.Withdrawn],
        [
            new([SignupApplicationStatus.Submitted], "approve", SignupApplicationStatus.Provisioning),
            new([SignupApplicationStatus.Provisioning], "provisioned", SignupApplicationStatus.Approved),
            new([SignupApplicationStatus.Provisioning], "fail", SignupApplicationStatus.Failed),
            new([SignupApplicationStatus.Failed, SignupApplicationStatus.Provisioning], "retry", SignupApplicationStatus.Provisioning),
            new([SignupApplicationStatus.Submitted], "reject", SignupApplicationStatus.Rejected),
            new([SignupApplicationStatus.Submitted], "withdraw", SignupApplicationStatus.Withdrawn),
        ],
        [
            new("submit", "applicant", "write", new(false, false)),
            new("withdraw", "applicant", "write", new(false, true)),
            new("approve", "operator", "write", new(false, true)),
            new("reject", "operator", "destructive", new(false, true)),
            new("retry", "operator", "write", new(false, false)),
        ]);
}
