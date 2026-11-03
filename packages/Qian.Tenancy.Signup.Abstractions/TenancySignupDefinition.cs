using Qian.Runtime;

namespace Qian.Tenancy.Signup;

/// <summary>租户自助开通 seam（S1-TEN-019）的服务键；组件为 <c>qian.tenancy.signup</c>，装进 identity profile。</summary>
public static class TenancySignupDefinition
{
    /// <summary>组件 ID。</summary>
    public const string ComponentId = "qian.tenancy.signup";
    /// <summary>申请、审核与开通对接服务（<see cref="ITenantSignup"/>）的服务键。</summary>
    public static readonly ServiceKey Key = new("TenancySignup");
    /// <summary>
    /// 发件端口（<see cref="ISignupMessenger"/>）的服务键，由通知组件或开发宿主提供。缺席时发验证码返回 503 SIGNUP_UNAVAILABLE，
    /// 审核结果通知跳过、不阻断审核。
    /// </summary>
    public static readonly ServiceKey MessengerKey = new("TenancySignupMessenger");
    /// <summary>在当前 scope 解析开通申请服务。</summary>
    public static ITenantSignup TenancySignup(this IPluginContext context) =>
        (context ?? throw new ArgumentNullException(nameof(context))).Inject<ITenantSignup>(Key);
}
