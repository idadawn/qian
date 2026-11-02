using System.Text.Json.Serialization;

namespace Qian.Tenancy.Signup;

/// <summary>开通申请状态（规格 §3）；approved、rejected、withdrawn 是终态，状态只进不退。</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SignupApplicationStatus>))]
public enum SignupApplicationStatus
{
    /// <summary>待审核：租户号预留，申请人可撤回，运营可通过或驳回。</summary>
    [JsonStringEnumMemberName("submitted")] Submitted,
    /// <summary>开通中：运营已通过，开通编排与激活执行中。</summary>
    [JsonStringEnumMemberName("provisioning")] Provisioning,
    /// <summary>已通过：租户已激活为试用或正式。</summary>
    [JsonStringEnumMemberName("approved")] Approved,
    /// <summary>开通失败：停在失败步骤，运营可重试；申请人一侧仍显示开通中。</summary>
    [JsonStringEnumMemberName("failed")] Failed,
    /// <summary>已驳回：租户号释放，原因对申请人可见。</summary>
    [JsonStringEnumMemberName("rejected")] Rejected,
    /// <summary>已撤回：租户号释放。</summary>
    [JsonStringEnumMemberName("withdrawn")] Withdrawn
}

/// <summary>审核通过时选择的开通方式。</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SignupPlan>))]
public enum SignupPlan
{
    /// <summary>试用：租户激活为 trial，到期日缺省为审核当日加组件配置的试用天数。</summary>
    [JsonStringEnumMemberName("trial")] Trial,
    /// <summary>正式：租户激活为 active，到期日可空。</summary>
    [JsonStringEnumMemberName("formal")] Formal
}

/// <summary>企业规模的取值（人数区间）。</summary>
public static class SignupCompanySizes
{
    /// <summary>1–20 人。</summary>
    public const string Micro = "1-20";
    /// <summary>21–100 人。</summary>
    public const string Small = "21-100";
    /// <summary>101–500 人。</summary>
    public const string Medium = "101-500";
    /// <summary>501–1000 人。</summary>
    public const string Large = "501-1000";
    /// <summary>1000 人以上。</summary>
    public const string Enterprise = "1000+";
    /// <summary>全部取值，按人数从小到大。</summary>
    public static IReadOnlyList<string> All { get; } = [Micro, Small, Medium, Large, Enterprise];
}

/// <summary>租户号不可用的原因（<see cref="SignupCodeAvailability.Reason"/>）。</summary>
public static class SignupCodeReasons
{
    /// <summary>格式或长度不合法（小写字母或数字，长度见组件配置）。</summary>
    public const string Invalid = "invalid";
    /// <summary>保留字。</summary>
    public const string Reserved = "reserved";
    /// <summary>已被已有租户或其它进行中的申请占用。</summary>
    public const string Taken = "taken";
}

/// <summary>自动检查的种类（规格 §5.2）。</summary>
public static class SignupCheckKinds
{
    /// <summary>租户号可用：未被已有租户或其它进行中的申请占用。</summary>
    public const string Code = "code";
    /// <summary>同名企业：已有租户或其它进行中的申请名称相同。</summary>
    public const string DuplicateName = "duplicateName";
    /// <summary>申请人账号已加入其它企业。</summary>
    public const string ExistingMemberships = "existingMemberships";
}

/// <summary>自动检查的级别，从轻到重；列表每行只带最需要关注的一条。</summary>
public static class SignupCheckLevels
{
    /// <summary>通过。</summary>
    public const string Pass = "pass";
    /// <summary>提示：无法判定或仅供参考。</summary>
    public const string Info = "info";
    /// <summary>警告：需要运营留意，但不阻止通过。</summary>
    public const string Warning = "warning";
    /// <summary>错误：照原样通过会失败，须先修正（例如租户号已被占用）。</summary>
    public const string Error = "error";
}
