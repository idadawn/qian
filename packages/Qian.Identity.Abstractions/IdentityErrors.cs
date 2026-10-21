using Qian.Runtime.Errors;

namespace Qian.Identity.Abstractions;

/// <summary>身份领域的稳定错误码，归属 qian.identity；协议级拒绝复用冻结内置码。</summary>
/// <remarks>登录失败的文案不区分「账号不存在」与「密码错误」，避免被用来枚举账号。</remarks>
public static class IdentityErrors
{
    private const string Owner = "qian.identity";

    /// <summary>账号或密码不正确（含账号不存在、凭据未设置），不泄露具体原因。</summary>
    public static readonly ErrorCode InvalidCredentials = Protocol("INVALID_CREDENTIALS", 401,
        "账号或密码不正确", "账号或密码不正确，请检查后重试。",
        "Invalid credentials", "The account or password is incorrect.");
    /// <summary>错误次数达到阈值，账号被限时锁定或延时；Retry-After 头给出等待秒数。</summary>
    public static readonly ErrorCode AccountLocked = Protocol("ACCOUNT_LOCKED", 423,
        "账号已锁定", "尝试次数过多，账号已被暂时锁定，请稍后重试或联系管理员。",
        "Account locked", "Too many failed attempts; the account is temporarily locked. Retry later or contact an administrator.", retryable: true);
    /// <summary>账号已被停用、锁定或清除；仅在凭据验证通过后返回。</summary>
    public static readonly ErrorCode AccountDisabled = Protocol("ACCOUNT_DISABLED", 403,
        "账号不可用", "该账号已被停用，请联系管理员。",
        "Account unavailable", "The account has been disabled; contact an administrator.");
    /// <summary>选择令牌或改密令牌无效、已过期或已使用。</summary>
    public static readonly ErrorCode LoginTicketInvalid = Protocol("LOGIN_TICKET_INVALID", 401,
        "登录票据无效", "登录票据无效、已过期或已被使用，请重新登录。",
        "Invalid login ticket", "The login ticket is invalid, expired or already used; sign in again.");
    /// <summary>目标租户处于待激活、宽限或冻结状态，拒绝新登录。</summary>
    public static readonly ErrorCode TenantNotActive = Protocol("TENANT_NOT_ACTIVE", 403,
        "租户不可登录", "该租户当前状态不允许登录，请联系租户管理员或平台运营。",
        "Tenant not available", "The tenant is not in a state that allows sign-in; contact the tenant administrator or platform operations.");
    /// <summary>凭据正确但没有任何可用的成员身份或平台资格。</summary>
    public static readonly ErrorCode NoAvailableTenant = Protocol("NO_AVAILABLE_TENANT", 403,
        "没有可用身份", "该账号没有可登录的租户成员身份，请联系租户管理员。",
        "No available membership", "The account has no tenant membership it can sign in to; contact the tenant administrator.");
    /// <summary>登录策略要求图形验证码，请求未携带。</summary>
    public static readonly ErrorCode CaptchaRequired = Protocol("CAPTCHA_REQUIRED", 428,
        "需要验证码", "请先完成图形验证码后再提交。",
        "Captcha required", "Solve the captcha before submitting.");
    /// <summary>图形验证码不正确或已过期。</summary>
    public static readonly ErrorCode CaptchaInvalid = Protocol("CAPTCHA_INVALID", 400,
        "验证码不正确", "图形验证码不正确或已过期，请刷新后重试。",
        "Invalid captcha", "The captcha is incorrect or has expired; refresh and retry.");
    /// <summary>账号已登记第二因子，但本实例没有可用的验证实现；登录一律拒绝，不降级为单因子。</summary>
    public static readonly ErrorCode MfaUnavailable = Protocol("MFA_UNAVAILABLE", 503,
        "二次验证不可用", "该账号需要二次验证，当前服务暂不可用，请稍后重试。",
        "Second factor unavailable", "The account requires a second factor, which is currently unavailable; retry later.");

    /// <summary>新密码不符合密码策略，rule 给出违反的规则。</summary>
    public static readonly ErrorCode PasswordPolicyViolation = Business("PASSWORD_POLICY_VIOLATION", 422,
        "密码不符合策略", "新密码不符合密码策略：{rule}。",
        "Password policy violated", "The new password does not satisfy the password policy: {rule}.");
    /// <summary>新密码与当前或最近使用过的密码相同。</summary>
    public static readonly ErrorCode PasswordReused = Business("PASSWORD_REUSED", 422,
        "密码不可复用", "新密码不能与最近使用过的 {count} 个密码相同。",
        "Password reused", "The new password must differ from your last {count} passwords.");
    /// <summary>当前密码不正确。</summary>
    public static readonly ErrorCode CurrentPasswordInvalid = Business("CURRENT_PASSWORD_INVALID", 422,
        "当前密码不正确", "当前密码不正确。",
        "Current password incorrect", "The current password is incorrect.");
    /// <summary>标识（手机、邮箱、用户名）或外部身份已被占用。</summary>
    public static readonly ErrorCode IdentifierInUse = Business("IDENTIFIER_IN_USE", 409,
        "标识已被占用", "该标识已被其他账号绑定。",
        "Identifier in use", "The identifier is already bound to another account.");
    /// <summary>解绑会让账号失去最后一个已验证标识（或全部标识）而无法找回，拒绝。</summary>
    public static readonly ErrorCode LastVerifiedIdentifier = Business("LAST_VERIFIED_IDENTIFIER", 422,
        "不能解绑最后一个已验证标识", "这是账号最后一个已验证的联系方式，解绑后将无法找回账号；请先绑定并验证新的标识。",
        "Last verified identifier", "This is the last verified contact of the account and removing it would make it unrecoverable; bind and verify a new one first.");
    /// <summary>解绑会让账号没有任何可用登录方式。</summary>
    public static readonly ErrorCode LastLoginMethod = Business("LAST_LOGIN_METHOD", 422,
        "不能解绑最后一种登录方式", "这是账号唯一的登录方式，解绑后将无法登录；请先设置密码或绑定其他登录方式。",
        "Last login method", "This is the only way to sign in; set a password or link another login method first.");
    /// <summary>租户内账号名已被占用。</summary>
    public static readonly ErrorCode AccountInUse = Business("ACCOUNT_IN_USE", 409,
        "账号名已被占用", "该租户内账号名已被其他成员使用。",
        "Account name in use", "The account name is already used by another member of this tenant.");

    /// <summary>可登记和导出的完整错误词汇。</summary>
    public static IReadOnlyList<ErrorCode> All { get; } = Array.AsReadOnly(new[]
    {
        InvalidCredentials, AccountLocked, AccountDisabled, LoginTicketInvalid, TenantNotActive, NoAvailableTenant, CaptchaRequired, CaptchaInvalid, MfaUnavailable,
        PasswordPolicyViolation, PasswordReused, CurrentPasswordInvalid, IdentifierInUse, LastVerifiedIdentifier, LastLoginMethod, AccountInUse
    });

    /// <summary>成组登记本组件错误码；失败撤销已登记项，返回可交给 ctx.Effect 的幂等释放句柄。</summary>
    public static IDisposable Register(ErrorCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var registrations = new List<IDisposable>();
        try
        {
            foreach (var code in All) registrations.Add(catalog.Register(code));
            return new Registration(registrations);
        }
        catch
        {
            foreach (var registration in registrations.AsEnumerable().Reverse()) registration.Dispose();
            throw;
        }
    }

    private static ErrorCode Protocol(string code, int status, string titleZh, string messageZh, string titleEn, string messageEn, bool retryable = false) =>
        ErrorCode.Protocol(code, status, Owner,
            new Dictionary<string, string> { ["zh-CN"] = titleZh, ["en"] = titleEn },
            new Dictionary<string, string> { ["zh-CN"] = messageZh, ["en"] = messageEn }, retryable);

    private static ErrorCode Business(string code, int status, string titleZh, string messageZh, string titleEn, string messageEn) =>
        ErrorCode.Business(code, status, Owner,
            new Dictionary<string, string> { ["zh-CN"] = titleZh, ["en"] = titleEn },
            new Dictionary<string, string> { ["zh-CN"] = messageZh, ["en"] = messageEn });

    private sealed class Registration(List<IDisposable> entries) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            foreach (var entry in entries.AsEnumerable().Reverse()) entry.Dispose();
        }
    }
}
