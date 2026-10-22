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
    /// <summary>上游连接器登录与外部身份绑定的流程参数。</summary>
    public ConnectorPolicy Connectors { get; init; } = new();
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
        Session.Validate();
        Connectors.Validate();
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

/// <summary>会话策略：绝对寿命、并发上限、单端模式，以及会话中心后台处理的节奏。</summary>
public sealed class SessionPolicy
{
    /// <summary>会话绝对寿命，超过后必须重新登录，与 refresh 轮换无关。</summary>
    public TimeSpan AbsoluteLifetime { get; init; } = TimeSpan.FromDays(30);

    /// <summary>同一 Person 同时有效的会话数上限（跨租户、跨端合计）；超过时新登录踢掉最早建立的会话。缺省 5。</summary>
    public int MaxConcurrentSessions { get; init; } = 5;

    /// <summary>
    /// 单端模式：同一 Person 在同一端类型（如 pc、mobile，取自客户端登记的 <c>DeviceType</c>）上只保留最新登录的一个会话，
    /// 后登录的会话踢掉同端的先登录会话，不影响其他端。缺省关闭；开启后并发上限仍然适用。
    /// </summary>
    public bool SingleSessionPerDeviceType { get; init; }

    /// <summary>
    /// 租户状态对账的间隔：后台核对仍有会话的租户在目录里的状态，不再是 Active / Trial 的租户其会话一律吊销。
    /// 这是 <c>tenant.suspended.v1</c> / <c>tenant.expired.v1</c> 订阅之外的兜底，使「租户冻结 / 到期后会话 ≤ 60 秒失效」不依赖消息投递。
    /// 不得超过 30 秒，缺省 30 秒。
    /// </summary>
    public TimeSpan ReconcileInterval { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>在线用户的判定窗口：最近一次活动（授权、换票、刷新）落在窗口内的有效会话才算在线。缺省 15 分钟，即访问令牌寿命。</summary>
    public TimeSpan OnlineWindow { get; init; } = TimeSpan.FromMinutes(15);

    internal void Validate()
    {
        if (AbsoluteLifetime <= TimeSpan.Zero) throw new ArgumentException("会话策略无效：绝对寿命必须为正。", nameof(SessionPolicy));
        if (MaxConcurrentSessions is < 1 or > 1000) throw new ArgumentException("会话策略无效：并发上限取 1–1000。", nameof(SessionPolicy));
        if (ReconcileInterval < TimeSpan.FromSeconds(1) || ReconcileInterval > TimeSpan.FromSeconds(30))
            throw new ArgumentException("会话策略无效：租户对账间隔取 1–30 秒（租户冻结后会话须在 60 秒内失效）。", nameof(SessionPolicy));
        if (OnlineWindow < TimeSpan.FromMinutes(1) || OnlineWindow > TimeSpan.FromDays(1)) throw new ArgumentException("会话策略无效：在线窗口取 1 分钟–1 天。", nameof(SessionPolicy));
    }
}

/// <summary>
/// 连接器登录流程（PRD 7.4.2、ADR-005 D5.11）：浏览器在上游完成认证的时限、确认绑定票据的寿命、绑定前「新鲜认证」的窗口，
/// 以及回调处理后 302 回到的界面落点。JIT 建号是否允许不在这里：它是租户策略，缺省关闭。
/// </summary>
public sealed class ConnectorPolicy
{
    /// <summary>从发起到上游回调的时限；过期的 state 按无效处理。缺省 10 分钟。</summary>
    public TimeSpan FlowLifetime { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>确认绑定的票据寿命，从回调起算；过期返回 AUTH_BINDING_EXPIRED。缺省 10 分钟。</summary>
    public TimeSpan BindingLifetime { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>确认绑定前登录已有账号（新鲜一因子认证）的有效窗口；超过需重新登录该账号。缺省 5 分钟。</summary>
    public TimeSpan ReauthenticationWindow { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>回调处理后 302 回到的界面落点（同源路径，含 <c>{connectorId}</c>），流程标识以 <c>?flow=</c> 附上。</summary>
    public string LandingPath { get; init; } = "/login/connector/{connectorId}";

    internal void Validate()
    {
        static bool Bad(TimeSpan value) => value < TimeSpan.FromMinutes(1) || value > TimeSpan.FromHours(1);
        if (Bad(FlowLifetime) || Bad(BindingLifetime) || Bad(ReauthenticationWindow) || ReauthenticationWindow > BindingLifetime)
            throw new ArgumentException("连接器流程策略无效：各时限取 1 分钟–1 小时，且新鲜认证窗口不长于绑定票据寿命。", nameof(ConnectorPolicy));
        if (string.IsNullOrEmpty(LandingPath) || !LandingPath.StartsWith('/') || LandingPath.StartsWith("//", StringComparison.Ordinal) || LandingPath.Contains('\\', StringComparison.Ordinal) ||
            LandingPath.Contains('?', StringComparison.Ordinal) || LandingPath.Contains('#', StringComparison.Ordinal) || !LandingPath.Contains("{connectorId}", StringComparison.Ordinal))
            throw new ArgumentException("连接器流程策略无效：界面落点必须是含 {connectorId} 的同源路径。", nameof(ConnectorPolicy));
    }
}
