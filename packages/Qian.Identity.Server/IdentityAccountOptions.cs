namespace Qian.Identity.Server;

/// <summary>
/// 账号模型与统一登录流水线的宿主参数。租户级覆盖（登录策略与密码策略 Schema，经 ITenantSettings）由 S1-IDP-006 接入前，
/// 全平台共用这里的取值。
/// </summary>
public sealed class IdentityAccountOptions
{
    /// <summary>Argon2id 成本参数。</summary>
    public Argon2idOptions Hashing { get; init; } = new();
    /// <summary>密码策略。</summary>
    public PasswordPolicy Password { get; init; } = new();
    /// <summary>登录失败、锁定与验证码策略。</summary>
    public LoginPolicy Login { get; init; } = new();
    /// <summary>会话策略。</summary>
    public SessionPolicy Session { get; init; } = new();
    /// <summary>手机号未带国家码时补的国家码，缺省中国大陆。</summary>
    public string DefaultPhoneCountryCode { get; init; } = "86";
    /// <inheritdoc />
    public override string ToString() => "IdentityAccountOptions(redacted)";

    internal void Validate()
    {
        Hashing.Validate();
        if (Password.MinLength < 8 || Password.MaxLength < Password.MinLength || Password.MaxLength > 1024 || Password.HistorySize is < 1 or > 24)
            throw new ArgumentException("密码策略无效：最小长度不得低于 8，历史条数 1–24。", nameof(Password));
        if (Login.MaxFailedAttempts is < 3 or > 100 || Login.LockoutDuration <= TimeSpan.Zero || Login.DelayBase <= TimeSpan.Zero ||
            Login.MaxDelay < Login.DelayBase || Login.FailureWindow <= TimeSpan.Zero || Login.CaptchaAfterFailures < 1 ||
            Login.SelectionLifetime <= TimeSpan.Zero || Login.SelectionLifetime > TimeSpan.FromMinutes(5) || Login.ChangeTicketLifetime <= TimeSpan.Zero)
            throw new ArgumentException("登录策略无效：阈值至少 3 次，选择令牌寿命不得超过 5 分钟。", nameof(Login));
        if (Session.AbsoluteLifetime <= TimeSpan.Zero) throw new ArgumentException("会话策略无效。", nameof(Session));
        if (DefaultPhoneCountryCode.Length is < 1 or > 3 || DefaultPhoneCountryCode.Any(c => c is < '0' or > '9'))
            throw new ArgumentException("默认国家码无效。", nameof(DefaultPhoneCountryCode));
    }
}

/// <summary>Argon2id 成本参数。缺省取 OWASP 的最低推荐组合（19 MiB、2 次迭代、1 并行度）。</summary>
public sealed class Argon2idOptions
{
    /// <summary>内存，单位 KiB。</summary>
    public int MemoryKiB { get; init; } = 19456;
    /// <summary>迭代次数。</summary>
    public int Iterations { get; init; } = 2;
    /// <summary>并行度。</summary>
    public int Parallelism { get; init; } = 1;
    /// <summary>盐长度，字节。</summary>
    public int SaltBytes { get; init; } = 16;
    /// <summary>摘要长度，字节。</summary>
    public int HashBytes { get; init; } = 32;

    internal void Validate()
    {
        if (MemoryKiB < 8 * Parallelism || Iterations < 1 || Parallelism < 1 || SaltBytes < 16 || HashBytes < 16)
            throw new ArgumentException("Argon2id 参数无效：盐至少 16 字节，摘要至少 16 字节。", nameof(Argon2idOptions));
    }
}

/// <summary>密码策略。长度按字符数计，比较前已做 NFKC 规范化。</summary>
public sealed class PasswordPolicy
{
    /// <summary>最小长度，不得低于 8。</summary>
    public int MinLength { get; init; } = 8;
    /// <summary>最大长度，防止超长输入拖慢哈希。</summary>
    public int MaxLength { get; init; } = 128;
    /// <summary>不得复用的最近密码条数，含当前密码。</summary>
    public int HistorySize { get; init; } = 5;
}

/// <summary>错误次数达到阈值后的处置方式。</summary>
public enum LockoutMode
{
    /// <summary>锁定账号一段固定时间，期间不再验证密码。</summary>
    Lock,
    /// <summary>累进延时：每多错一次，下次允许尝试的间隔翻倍，封顶 <see cref="LoginPolicy.MaxDelay"/>。</summary>
    Delay
}

/// <summary>图形验证码的开关策略。</summary>
public enum CaptchaMode
{
    /// <summary>关闭。</summary>
    Off,
    /// <summary>每次登录都要求。</summary>
    Always,
    /// <summary>该账号连续失败达到 <see cref="LoginPolicy.CaptchaAfterFailures"/> 次后要求。</summary>
    AfterFailures
}

/// <summary>登录策略。</summary>
public sealed class LoginPolicy
{
    /// <summary>触发锁定或延时的连续错误次数（含），至少 3。</summary>
    public int MaxFailedAttempts { get; init; } = 5;
    /// <summary>处置方式。</summary>
    public LockoutMode Mode { get; init; } = LockoutMode.Lock;
    /// <summary>Lock 模式的锁定时长。</summary>
    public TimeSpan LockoutDuration { get; init; } = TimeSpan.FromMinutes(15);
    /// <summary>Delay 模式的起始延时。</summary>
    public TimeSpan DelayBase { get; init; } = TimeSpan.FromSeconds(5);
    /// <summary>Delay 模式的延时上限。</summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromMinutes(15);
    /// <summary>上一次失败距今超过此窗口，错误次数重新计数。</summary>
    public TimeSpan FailureWindow { get; init; } = TimeSpan.FromMinutes(15);
    /// <summary>图形验证码开关。</summary>
    public CaptchaMode Captcha { get; init; } = CaptchaMode.Off;
    /// <summary>AfterFailures 模式下要求验证码的失败次数。</summary>
    public int CaptchaAfterFailures { get; init; } = 3;
    /// <summary>租户选择令牌寿命，PRD 规定 5 分钟，不可放宽。</summary>
    public TimeSpan SelectionLifetime { get; init; } = TimeSpan.FromMinutes(5);
    /// <summary>强制改密令牌寿命。</summary>
    public TimeSpan ChangeTicketLifetime { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>第 <paramref name="failed"/> 次连续错误之后的锁定时长；未达阈值返回 null。</summary>
    public TimeSpan? LockoutFor(int failed)
    {
        if (failed < MaxFailedAttempts) return null;
        if (Mode == LockoutMode.Lock) return LockoutDuration;
        var doublings = Math.Min(failed - MaxFailedAttempts, 30);
        var seconds = DelayBase.TotalSeconds * Math.Pow(2, doublings);
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxDelay.TotalSeconds));
    }
}

/// <summary>会话策略；并发上限与单端踢出由会话中心（S1-IDP-004）接入。</summary>
public sealed class SessionPolicy
{
    /// <summary>会话绝对寿命，超过后必须重新登录，与 refresh 轮换无关。</summary>
    public TimeSpan AbsoluteLifetime { get; init; } = TimeSpan.FromDays(30);
}
