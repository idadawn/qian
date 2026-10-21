using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Qian.Events;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors;
using Qian.Tenancy;
using Qian.Testing.Containers;
using static Qian.Identity.Server.Tests.AccountSupport;

namespace Qian.Identity.Server.Tests;

// 统一登录流水线：真实 PostgreSQL（受限应用角色 + 强制 RLS）、真实服务实现。
// 时间由可调时钟给出，锁定到期、票据过期、会话寿命都靠推进时钟验证，不靠睡眠。
public sealed partial class SignInPipelineTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly LoginContext Net = new("203.0.113.9", "UT-Agent/1.0");

    private static IdentityAccountOptions Policy(int max = 5, LockoutMode mode = LockoutMode.Lock, TimeSpan? delayBase = null, TimeSpan? maxDelay = null, TimeSpan? window = null,
        CaptchaMode captcha = CaptchaMode.Off, int captchaAfter = 3) => new()
    {
        Login = new LoginPolicy
        {
            MaxFailedAttempts = max, Mode = mode, DelayBase = delayBase ?? TimeSpan.FromSeconds(5), MaxDelay = maxDelay ?? TimeSpan.FromMinutes(15),
            FailureWindow = window ?? TimeSpan.FromMinutes(15), Captcha = captcha, CaptchaAfterFailures = captchaAfter
        }
    };

    private static Task<SignInResult> Wrong(AccountsHost.Running host, string phone) => host.SignIn.SignInAsync(Input(phone, "Wrong-Pass-1"), Net, Ct).AsTask();

    private static Task<SignInResult> Right(AccountsHost.Running host, string phone, string password = Password) => host.SignIn.SignInAsync(Input(phone, password), Net, Ct).AsTask();

    private static Task<SignInResult> Pick(AccountsHost.Running host, LoginTicket ticket, Guid? membership) => host.SignIn.SelectAsync(new TenantSelection(ticket.Token, membership), Net, Ct).AsTask();

    // 失败时返回稳定错误码；没有失败则返回状态，断言失败时一眼看出实际发生了什么。
    private static string Code(SignInResult result) => result.Error?.Code.Code ?? $"<{result.Status}>";

    private static string Id(Guid id) => id.ToString("D");

    private static Task<string> PersonValue(AccountsHost.Running host, Guid person, string expression) =>
        host.Text($"SELECT {expression} FROM plugin_qian_identity.persons WHERE id = @p::uuid", ("p", Id(person)));

    private static async Task<IReadOnlyList<QianEventEnvelope>> Events(AccountsHost.Running host, string type)
    {
        var joined = await host.Text("SELECT coalesce(string_agg(payload, E'\\n' ORDER BY occurred_at, id), '') FROM plugin_qian_identity.outbox_messages WHERE event_type_id = @t", ("t", type));
        return joined.Length == 0 ? [] : [.. joined.Split('\n').Select(QianEventEnvelope.Parse)];
    }

    // 同一个人在 alpha、beta 两个租户各有一个成员身份。
    private sealed record World(AccountsHost.Running Host, TenantDescriptor A, TenantDescriptor B, Person Person, string Phone, Membership InA, Membership InB) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Host.DisposeAsync();
    }

    private static async Task<World> TwoTenants(PostgresFixture postgres, IdentityAccountOptions? options = null)
    {
        var host = await AccountsHost.Start(postgres, options);
        var a = host.Tenants.Add("alpha"); var b = host.Tenants.Add("beta");
        var (person, phone) = await CreatePerson(host);
        return new World(host, a, b, person, phone, await Join(host, a, person.Id, "甲方成员"), await Join(host, b, person.Id, "乙方成员"));
    }

    [Fact]
    public async Task 密码登录建立会话_一次调用恰好一条审计_登录事件在所属租户作用域内入Outbox_审计与事件不含凭据()
    {
        await using var host = await AccountsHost.Start(postgres);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        var member = await Join(host, tenant, person.Id);
        var result = await Right(host, phone);
        Assert.True(result.Succeeded); Assert.Equal(SignInStatus.Authenticated, result.Status);
        var session = result.Session!;
        Assert.Equal((person.Id, tenant.Id, member.Id), (session.PersonId, session.TenantId, session.MembershipId));
        Assert.Equal(("web", "pc", "pwd"), (session.ClientId, session.DeviceType, Assert.Single(session.Amr)));
        Assert.Null(session.Acr);
        Assert.Equal(host.Clock.GetUtcNow(), session.AuthenticatedAt);
        Assert.Equal(session.AuthenticatedAt + TimeSpan.FromDays(30), session.ExpiresAt);
        Assert.Equal(0, session.IdentityRevision);
        Assert.Equal("1", await host.Text("""
            SELECT count(*) FROM plugin_qian_identity.sessions
            WHERE id = @sid::uuid AND person_id = @p::uuid AND membership_id = @m::uuid AND tenant_id = @t AND client_id = 'web' AND device_type = 'pc'
              AND amr = ARRAY['pwd']::text[] AND revoked_at IS NULL AND remote_address = '203.0.113.9' AND user_agent = 'UT-Agent/1.0'
            """, ("sid", Id(session.SessionId)), ("p", Id(person.Id)), ("m", Id(member.Id)), ("t", Id(tenant.Id))));

        // 一次登录请求恰好一条审计：标识只留类型与掩码，sid 只留哈希。
        Assert.Equal("1", await host.Text("SELECT count(*) FROM plugin_qian_identity.login_audits"));
        var audit = await host.Text("""
            SELECT concat_ws('|', stage, outcome, coalesce(reason, '-'), person_id, membership_id, tenant_id, identifier_kind, identifier_masked, client_id, method, remote_address, user_agent, session_hash)
            FROM plugin_qian_identity.login_audits
            """);
        var sidHash = Convert.ToHexStringLower(SHA256.HashData(session.SessionId.ToByteArray()))[..16];
        Assert.Equal($"authenticate|success|-|{Id(person.Id)}|{Id(member.Id)}|{Id(tenant.Id)}|phone|{phone[..3]}****{phone[^4..]}|web|password|203.0.113.9|UT-Agent/1.0|{sidHash}", audit);
        Assert.DoesNotContain(phone, audit, StringComparison.Ordinal); Assert.DoesNotContain(Id(session.SessionId), audit, StringComparison.Ordinal);
        Assert.Equal("true", await PersonValue(host, person.Id, "(last_login_at IS NOT NULL AND failed_attempts = 0)::text"));

        // 登录事件与会话同事务入 Outbox：信封租户是所属租户，来源是 qian.identity，行为主体是该用户；载荷不含令牌或凭据。
        var envelope = Assert.Single(await Events(host, "identity.login.v1"));
        Assert.Equal(tenant.Id, envelope.TenantId); Assert.Equal("qian.identity", envelope.Producer);
        Assert.Equal((EventActorKind.User, Id(person.Id)), (envelope.Actor.Kind, envelope.Actor.Id));
        Assert.Equal(Id(session.SessionId), envelope.Payload.GetProperty("session_id").GetString());
        Assert.Equal(Id(member.Id), envelope.Payload.GetProperty("membership_id").GetString());
        Assert.Equal("web", envelope.Payload.GetProperty("client_id").GetString());
        var raw = await host.Text("SELECT payload FROM plugin_qian_identity.outbox_messages WHERE event_type_id = 'identity.login.v1'");
        Assert.DoesNotContain(Password, raw, StringComparison.Ordinal); Assert.DoesNotContain("argon2", raw, StringComparison.Ordinal);
        Assert.Equal(Id(tenant.Id), await host.Text("SELECT tenant_id FROM plugin_qian_identity.outbox_messages WHERE event_type_id = 'identity.login.v1'"));
    }

    [Fact]
    public async Task 失败也恰好一条审计_账号不存在_密码错误_格式非法对外同一错误_缺参数与不受支持的方式明确拒绝且不消耗密码尝试()
    {
        await using var host = await AccountsHost.Start(postgres);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        var wrong = await Wrong(host, phone);
        var unknown = await host.SignIn.SignInAsync(Input("13900000001", "Wrong-Pass-1"), Net, Ct);
        var malformed = await host.SignIn.SignInAsync(Input("not a valid id", "Wrong-Pass-1"), Net, Ct);
        foreach (var failed in new[] { wrong, unknown, malformed })
        {
            Assert.False(failed.Succeeded); Assert.Equal(SignInStatus.Failed, failed.Status);
            Assert.Equal("INVALID_CREDENTIALS", failed.Error!.Code.Code); Assert.Equal(401, failed.Error.Code.Status);
            Assert.Empty(failed.Error.Args); Assert.Empty(failed.Error.Errors); Assert.Null(failed.Session); Assert.Null(failed.Ticket); Assert.Null(failed.RetryAfter);
        }
        Assert.Equal("3", await host.Text("SELECT count(*) FROM plugin_qian_identity.login_audits WHERE outcome = 'failure'"));
        Assert.Equal("1", await host.Text("SELECT count(*) FROM plugin_qian_identity.login_audits WHERE reason = 'bad_credentials' AND person_id = @p::uuid", ("p", Id(person.Id))));
        Assert.Equal("2", await host.Text("SELECT count(*) FROM plugin_qian_identity.login_audits WHERE reason = 'unknown_account' AND person_id IS NULL"));
        Assert.Equal("len=14", await host.Text("SELECT identifier_masked FROM plugin_qian_identity.login_audits WHERE identifier_kind = 'invalid'"));
        Assert.Equal("139****0001", await host.Text("SELECT identifier_masked FROM plugin_qian_identity.login_audits WHERE identifier_kind = 'phone' AND person_id IS NULL"));
        Assert.Equal("0", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions"));
        Assert.Equal("1", await PersonValue(host, person.Id, "failed_attempts::text"));

        // 请求本身不完整或指向不存在的方式：400 + 出错位置，同样审计，且不消耗该账号的密码尝试。
        async Task<string?> Pointer(LoginInput input) => (await host.SignIn.SignInAsync(input, Net, Ct)).Error!.Errors.Single().Pointer;
        Assert.Equal("#/client_id", await Pointer(Input(phone, Password, "evil")));
        Assert.Equal("#/client_id", await Pointer(Input(phone, Password, "machine"))); // 机器客户端不能用来登录用户
        Assert.Equal("#/proof", await Pointer(new LoginInput("web", null, null, null, null) { Identifier = phone }));
        Assert.Equal("#/connector_id", await Pointer(Input(phone, Password) with { ConnectorId = "wecom" }));
        Assert.Equal("#/factor_id", await Pointer(Input(phone, Password) with { FactorId = "sms" }));
        Assert.Equal("8", await host.Text("SELECT count(*) FROM plugin_qian_identity.login_audits"));
        Assert.Equal("1", await PersonValue(host, person.Id, "failed_attempts::text"));
        // 凭据对了之后才暴露账号状态：停用账号输错密码仍是同一个错误，输对才知道已停用。
        await host.Accounts.SetPersonStatusAsync(person.Id, AccountStatuses.Disabled, "ops", Ct);
        Assert.Equal("INVALID_CREDENTIALS", Code(await Wrong(host, phone)));
        var disabled = await Right(host, phone);
        Assert.Equal("ACCOUNT_DISABLED", Code(disabled)); Assert.Equal(403, disabled.Error!.Code.Status);
    }

    [Fact]
    public async Task 错误次数达到阈值触发锁定_锁定期间正确密码也不验证_到期后恢复且成功登录清零()
    {
        await using var host = await AccountsHost.Start(postgres, Policy(max: 3, window: TimeSpan.FromMinutes(10)));
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        Assert.Equal("INVALID_CREDENTIALS", Code(await Wrong(host, phone))); host.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("INVALID_CREDENTIALS", Code(await Wrong(host, phone))); host.Clock.Advance(TimeSpan.FromSeconds(1));
        var locked = await Wrong(host, phone);
        Assert.Equal("ACCOUNT_LOCKED", Code(locked)); Assert.Equal(423, locked.Error!.Code.Status); Assert.True(locked.Error.Code.Retryable);
        Assert.Equal(TimeSpan.FromMinutes(15), locked.RetryAfter);
        var lockEvent = Assert.Single(await Events(host, "identity.locked.v1"));
        Assert.Equal("too_many_attempts", lockEvent.Payload.GetProperty("reason").GetString());
        Assert.Equal(host.Clock.GetUtcNow() + TimeSpan.FromMinutes(15), lockEvent.Payload.GetProperty("locked_until").GetDateTimeOffset());
        Assert.Equal(Id(person.Id), lockEvent.Payload.GetProperty("person_id").GetString());
        Assert.Null(lockEvent.TenantId); // 账号不属于任何租户，锁定是平台级事实

        // 锁定期间连正确密码也不验证：不增加计数、不签发，剩余时间随时钟递减。
        host.Clock.Advance(TimeSpan.FromSeconds(1));
        var during = await Right(host, phone);
        Assert.Equal("ACCOUNT_LOCKED", Code(during)); Assert.Equal(TimeSpan.FromMinutes(15) - TimeSpan.FromSeconds(1), during.RetryAfter);
        Assert.Equal("3", await PersonValue(host, person.Id, "failed_attempts::text"));
        host.Clock.Advance(TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(58)); // 距解锁还差 1 秒
        Assert.Equal("ACCOUNT_LOCKED", Code(await Right(host, phone)));
        host.Clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(SignInStatus.Authenticated, (await Right(host, phone)).Status);
        Assert.Equal("0|", await PersonValue(host, person.Id, "failed_attempts::text || '|' || coalesce(lockout_until::text, '')"));
        Assert.Single(await Events(host, "identity.locked.v1")); // 到期恢复不重复通知
        // 每次调用恰好一条审计：3 次失败（含越线那次）+ 2 次锁定期间被拒 + 1 次成功。
        Assert.Equal("6", await host.Text("SELECT count(*) FROM plugin_qian_identity.login_audits"));
        Assert.Equal("2", await host.Text("SELECT count(*) FROM plugin_qian_identity.login_audits WHERE reason = 'locked'"));
        Assert.Equal("1", await host.Text("SELECT count(*) FROM plugin_qian_identity.login_audits WHERE reason = 'locked_now'"));
    }

    [Fact]
    public async Task 阈值那次尝试若输对则放行并清零_不锁定也不发锁定事件_窗口外的旧失败不累计()
    {
        await using var host = await AccountsHost.Start(postgres, Policy(max: 3, window: TimeSpan.FromMinutes(10)));
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        await Wrong(host, phone); await Wrong(host, phone);
        Assert.Equal(SignInStatus.Authenticated, (await Right(host, phone)).Status); // 第 3 次尝试（阈值）输对
        Assert.Equal("0|", await PersonValue(host, person.Id, "failed_attempts::text || '|' || coalesce(lockout_until::text, '')"));
        Assert.Empty(await Events(host, "identity.locked.v1"));
        // 清零是真的：之后再错一次从 1 算起，不会被旧的两次累计锁住。
        Assert.Equal("INVALID_CREDENTIALS", Code(await Wrong(host, phone)));
        Assert.Equal("1", await PersonValue(host, person.Id, "failed_attempts::text"));
        // 两次错误之后隔过计数窗口再错一次，只算第一次。
        await Wrong(host, phone);
        host.Clock.Advance(TimeSpan.FromMinutes(11));
        Assert.Equal("INVALID_CREDENTIALS", Code(await Wrong(host, phone)));
        Assert.Equal("1", await PersonValue(host, person.Id, "failed_attempts::text"));
        Assert.Empty(await Events(host, "identity.locked.v1"));
    }

    [Fact]
    public async Task 延时模式每越线一次延时翻倍并封顶_延时期内不验证_只在首次越线发锁定事件()
    {
        await using var host = await AccountsHost.Start(postgres, Policy(max: 3, mode: LockoutMode.Delay, delayBase: TimeSpan.FromSeconds(5), maxDelay: TimeSpan.FromSeconds(40), window: TimeSpan.FromMinutes(10)));
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        await Wrong(host, phone); host.Clock.Advance(TimeSpan.FromSeconds(1));
        await Wrong(host, phone); host.Clock.Advance(TimeSpan.FromSeconds(1));
        var first = await Wrong(host, phone);
        Assert.Equal(("ACCOUNT_LOCKED", TimeSpan.FromSeconds(5)), (Code(first), first.RetryAfter!.Value));
        host.Clock.Advance(TimeSpan.FromSeconds(1));
        var early = await Right(host, phone); // 延时期内连正确密码也不验证
        Assert.Equal(("ACCOUNT_LOCKED", TimeSpan.FromSeconds(4)), (Code(early), early.RetryAfter!.Value));
        foreach (var expected in new[] { 10, 20, 40, 40 })
        {
            host.Clock.Advance(TimeSpan.FromSeconds(60)); // 过了上一个延时，窗口仍有效，计数继续累加
            var next = await Wrong(host, phone);
            Assert.Equal(("ACCOUNT_LOCKED", TimeSpan.FromSeconds(expected)), (Code(next), next.RetryAfter!.Value));
        }
        Assert.Single(await Events(host, "identity.locked.v1"));
        host.Clock.Advance(TimeSpan.FromSeconds(41));
        Assert.Equal(SignInStatus.Authenticated, (await Right(host, phone)).Status);
        Assert.Equal("0", await PersonValue(host, person.Id, "failed_attempts::text"));
    }

    [Fact]
    public async Task 并发猜测不能越过阈值_占位在先_恰好阈值次尝试被验证_其余直接被锁定拒绝_只发一次锁定事件()
    {
        await using var host = await AccountsHost.Start(postgres, Policy(max: 5));
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Wrong(host, phone)));
        Assert.Equal(4, results.Count(r => Code(r) == "INVALID_CREDENTIALS"));
        Assert.Equal(16, results.Count(r => Code(r) == "ACCOUNT_LOCKED"));
        Assert.Equal("4", await host.Text("SELECT count(*) FROM plugin_qian_identity.login_audits WHERE reason = 'bad_credentials'"));
        Assert.Equal("1", await host.Text("SELECT count(*) FROM plugin_qian_identity.login_audits WHERE reason = 'locked_now'"));
        Assert.Equal("15", await host.Text("SELECT count(*) FROM plugin_qian_identity.login_audits WHERE reason = 'locked'"));
        Assert.Equal("5", await PersonValue(host, person.Id, "failed_attempts::text"));
        Assert.Single(await Events(host, "identity.locked.v1"));
        Assert.Equal("ACCOUNT_LOCKED", Code(await Right(host, phone)));
    }

    [Fact]
    public async Task 初始密码账号登录后被强制改密且不可跳过_票据一次性且五分钟过期_弱密码与复用被拒_改密后继续同一登录流程()
    {
        await using var host = await AccountsHost.Start(postgres);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host, requireChange: true);
        var member = await Join(host, tenant, person.Id);
        var first = await Right(host, phone);
        Assert.Equal((false, SignInStatus.PasswordChangeRequired), (first.Succeeded, first.Status));
        Assert.Equal(LoginTicketPurpose.PasswordChange, first.Ticket!.Purpose); Assert.Null(first.Session);
        Assert.Equal(host.Clock.GetUtcNow() + TimeSpan.FromMinutes(5), first.Ticket.ExpiresAt);
        Assert.Equal("0", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions")); // 没有改密之前没有任何会话
        // 再登录一次仍然被要求改密——没有绕过的路径；改密令牌也不能当作选择令牌兑换会话。
        Assert.Equal(SignInStatus.PasswordChangeRequired, (await Right(host, phone)).Status);
        Assert.Equal("LOGIN_TICKET_INVALID", Code(await Pick(host, first.Ticket, member.Id)));
        Assert.Equal("0", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions"));

        async Task<SignInResult> Change(string ticket, string password)
        {
            using var proof = Proof(password);
            return await host.SignIn.ChangeInitialPasswordAsync(new InitialPasswordChange(ticket, proof), Net, Ct);
        }
        // 弱密码与复用初始密码：被拒绝，票据保留，仍可重试。
        var weak = await Change(first.Ticket.Token, "short");
        Assert.Equal(("PASSWORD_POLICY_VIOLATION", "minLength=8"), (Code(weak), weak.Error!.Args["rule"]));
        Assert.Equal("PASSWORD_REUSED", Code(await Change(first.Ticket.Token, Password)));
        Assert.Equal("true", await PersonValue(host, person.Id, "must_change_password::text"));
        Assert.Equal("0", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions"));
        var changed = await Change(first.Ticket.Token, "Brand-New-Pass-7");
        Assert.Equal(SignInStatus.Authenticated, changed.Status);
        Assert.Equal((tenant.Id, member.Id, "pwd"), (changed.Session!.TenantId, changed.Session.MembershipId, Assert.Single(changed.Session.Amr)));
        Assert.Equal("false|1", await PersonValue(host, person.Id, "must_change_password::text || '|' || identity_revision::text"));
        Assert.Single(await Events(host, "identity.password_changed.v1"));
        // 票据一次性；旧密码作废，新密码有效且不再强制改密。
        Assert.Equal("LOGIN_TICKET_INVALID", Code(await Change(first.Ticket.Token, "Another-New-Pass-8")));
        Assert.Equal("INVALID_CREDENTIALS", Code(await Right(host, phone)));
        Assert.Equal(SignInStatus.Authenticated, (await Right(host, phone, "Brand-New-Pass-7")).Status);

        // 改密令牌 5 分钟过期。
        var (other, otherPhone) = await CreatePerson(host, "李四", requireChange: true);
        await Join(host, tenant, other.Id);
        var pending = await Right(host, otherPhone);
        host.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        Assert.Equal("LOGIN_TICKET_INVALID", Code(await Change(pending.Ticket!.Token, "Brand-New-Pass-9")));
        // 格式不对的票据不进数据库查询，也不泄露任何信息。
        Assert.Equal("LOGIN_TICKET_INVALID", Code(await Change("x", "Brand-New-Pass-9")));
        Assert.Equal("LOGIN_TICKET_INVALID", Code(await Change(new string('z', 43), "Brand-New-Pass-9")));
        Assert.Equal("LOGIN_TICKET_INVALID", Code(await Change("../../etc/passwd" + new string('a', 30), "Brand-New-Pass-9")));
        Assert.Equal("true", await PersonValue(host, other.Id, "must_change_password::text"));
    }

    [Fact]
    public async Task 初始密码改完之后若有多个身份仍进入租户选择_强制改密不绕过也不吞掉选择()
    {
        await using var world = await TwoTenants(postgres);
        var (host, a, _, person, phone, inA, _) = world;
        await host.Exec("UPDATE plugin_qian_identity.persons SET must_change_password = true WHERE id = @p::uuid", ("p", Id(person.Id)));
        var pending = await Right(host, phone);
        Assert.Equal(SignInStatus.PasswordChangeRequired, pending.Status);
        using var proof = Proof("Brand-New-Pass-7");
        var next = await host.SignIn.ChangeInitialPasswordAsync(new InitialPasswordChange(pending.Ticket!.Token, proof), Net, Ct);
        Assert.Equal(SignInStatus.SelectionRequired, next.Status); Assert.Equal(2, next.Choices.Count);
        Assert.Equal(LoginTicketPurpose.Selection, next.Ticket!.Purpose);
        var established = await Pick(host, next.Ticket, inA.Id);
        Assert.Equal((SignInStatus.Authenticated, a.Id), (established.Status, established.Session!.TenantId));
        Assert.Equal(1, established.Session.IdentityRevision); // 改密使 Person 修订加一，会话从新修订开始
    }

    [Fact]
    public async Task 同一手机号在两个租户各有成员_密码登录后出现租户选择_选A得到A的会话_票据一次性且五分钟过期()
    {
        await using var world = await TwoTenants(postgres);
        var (host, a, b, person, phone, inA, inB) = world;
        var first = await Right(host, phone);
        Assert.Equal((false, SignInStatus.SelectionRequired), (first.Succeeded, first.Status));
        Assert.Equal(LoginTicketPurpose.Selection, first.Ticket!.Purpose); Assert.Null(first.Session);
        Assert.Equal(host.Clock.GetUtcNow() + TimeSpan.FromMinutes(5), first.Ticket.ExpiresAt);
        Assert.Equal(new[] { ("alpha", "ALPHA", "甲方成员", inA.Id), ("beta", "BETA", "乙方成员", inB.Id) },
            first.Choices.OrderBy(c => c.TenantCode).Select(c => (c.TenantCode!, c.TenantName!, c.DisplayName, c.MembershipId!.Value)));
        Assert.All(first.Choices, c => Assert.False(c.IsPlatform));
        Assert.Equal("0", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions"));
        Assert.Equal("selection_required", await host.Text("SELECT outcome FROM plugin_qian_identity.login_audits"));
        // 库里只有令牌的 SHA-256，没有令牌本身。
        var expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(first.Ticket.Token)));
        Assert.Equal("1", await host.Text("SELECT count(*) FROM plugin_qian_identity.login_tickets WHERE encode(token_hash, 'hex') = @h AND purpose = 'selection'", ("h", expected)));
        Assert.Equal("0", await host.Text("SELECT count(*) FROM plugin_qian_identity.login_tickets t WHERE t::text LIKE '%' || @t || '%'", ("t", first.Ticket.Token)));

        // 不在签发时确认过的集合内的成员（别人的、不存在的、平台身份）选不了，也不会烧掉票据。
        var (stranger, _) = await CreatePerson(host, "陌生人");
        var outsider = await Join(host, a, stranger.Id);
        var rejected = await Pick(host, first.Ticket, outsider.Id);
        Assert.Equal(("INPUT_INVALID", "#/membership_id"), (Code(rejected), rejected.Error!.Errors.Single().Pointer));
        Assert.Equal("INPUT_INVALID", Code(await Pick(host, first.Ticket, Guid.NewGuid())));
        Assert.Equal("INPUT_INVALID", Code(await Pick(host, first.Ticket, null))); // 不是平台用户，没有平台身份可选

        var selected = await Pick(host, first.Ticket, inA.Id);
        Assert.Equal(SignInStatus.Authenticated, selected.Status);
        Assert.Equal((person.Id, a.Id, inA.Id), (selected.Session!.PersonId, selected.Session.TenantId, selected.Session.MembershipId));
        Assert.Equal("pwd", Assert.Single(selected.Session.Amr));
        Assert.Equal("LOGIN_TICKET_INVALID", Code(await Pick(host, first.Ticket, inB.Id))); // 一次性
        // 登录事件落在选中的租户 A 的作用域内，而不是 B。
        Assert.Equal(a.Id, Assert.Single(await Events(host, "identity.login.v1")).TenantId);
        Assert.Equal("select", await host.Text("SELECT stage FROM plugin_qian_identity.login_audits WHERE outcome = 'success'"));

        // 选择令牌 5 分钟过期：差一秒还能用，过了一秒不行。
        var second = await Right(host, phone);
        host.Clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        Assert.Equal(b.Id, (await Pick(host, second.Ticket!, inB.Id)).Session!.TenantId);
        var third = await Right(host, phone);
        host.Clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        Assert.Equal("LOGIN_TICKET_INVALID", Code(await Pick(host, third.Ticket!, inA.Id)));
        // 登录时指定租户：只在该租户内选，唯一身份直接建立会话；指向没有身份的租户则无可用身份。
        var scoped = await host.SignIn.SignInAsync(Input(phone, Password, tenant: b.Id), Net, Ct);
        Assert.Equal((SignInStatus.Authenticated, b.Id, inB.Id), (scoped.Status, scoped.Session!.TenantId, scoped.Session.MembershipId));
        Assert.Equal("NO_AVAILABLE_TENANT", Code(await host.SignIn.SignInAsync(Input(phone, Password, tenant: Guid.NewGuid()), Net, Ct)));
    }

    [Fact]
    public async Task 选择令牌并发兑换只有一次成功_其余全部失败且只留一个会话()
    {
        await using var world = await TwoTenants(postgres);
        var (host, _, _, _, phone, inA, _) = world;
        var ticket = (await Right(host, phone)).Ticket!;
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Pick(host, ticket, inA.Id)));
        Assert.Equal(1, results.Count(r => r.Succeeded));
        Assert.Equal(7, results.Count(r => Code(r) == "LOGIN_TICKET_INVALID"));
        Assert.Equal("1", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions"));
        Assert.Single(await Events(host, "identity.login.v1"));
        Assert.Equal("1", await host.Text("SELECT count(*) FROM plugin_qian_identity.login_audits WHERE outcome = 'success'"));
    }

    [Fact]
    public async Task 在外壳切换租户_令牌重签且成员变化_旧会话同事务吊销_认证强度与绝对寿命不被提升()
    {
        await using var world = await TwoTenants(postgres);
        var (host, a, b, person, phone, inA, inB) = world;
        var inAlpha = (await Pick(host, (await Right(host, phone)).Ticket!, inA.Id)).Session!;
        host.Clock.Advance(TimeSpan.FromHours(2));
        var begin = await host.SignIn.BeginSwitchAsync(inAlpha, Net, Ct);
        Assert.Equal(SignInStatus.SelectionRequired, begin.Status); Assert.Equal(2, begin.Choices.Count); Assert.Null(begin.Session);
        Assert.NotNull(await host.Service.ValidateAsync(inAlpha.SessionId, person.Id, inA.Id, Ct)); // 选之前旧会话仍然有效
        var switched = (await Pick(host, begin.Ticket!, inB.Id)).Session!;
        Assert.Equal((b.Id, inB.Id), (switched.TenantId, switched.MembershipId));
        Assert.NotEqual(inAlpha.SessionId, switched.SessionId);
        Assert.NotEqual(inAlpha.MembershipId, switched.MembershipId);
        // 切换只换身份：认证时刻、方式、绝对寿命都沿用最初的登录，不因切换而延长。
        Assert.Equal(inAlpha.AuthenticatedAt, switched.AuthenticatedAt);
        Assert.Equal(inAlpha.ExpiresAt, switched.ExpiresAt);
        Assert.Equal(inAlpha.Amr, switched.Amr);
        Assert.Equal(inAlpha.Acr, switched.Acr);
        Assert.Equal("switch", await host.Text("SELECT revoke_reason FROM plugin_qian_identity.sessions WHERE id = @sid::uuid", ("sid", Id(inAlpha.SessionId))));
        Assert.Equal("1", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions WHERE revoked_at IS NULL"));
        Assert.Null(await host.Service.ValidateAsync(inAlpha.SessionId, person.Id, inA.Id, Ct));
        Assert.NotNull(await host.Service.ValidateAsync(switched.SessionId, person.Id, inB.Id, Ct));
        // 审计按阶段记录：登录选择、选 A、发起切换、切换完成各一条。
        Assert.Equal("authenticate/selection_required:1,select/success:1,switch/selection_required:1,switch/success:1", await host.Text("""
            SELECT string_agg(stage || '/' || outcome || ':' || n, ',' ORDER BY stage, outcome)
            FROM (SELECT stage, outcome, count(*) AS n FROM plugin_qian_identity.login_audits GROUP BY stage, outcome) s
            """));
        var logins = await Events(host, "identity.login.v1");
        Assert.Equal(new[] { a.Id, b.Id }, logins.Select(e => e.TenantId!.Value));

        // 已失效的会话不能发起切换，别人的会话、对不上身份的会话更不能。
        var stale = await host.SignIn.BeginSwitchAsync(inAlpha, Net, Ct);
        Assert.Equal(("UNAUTHENTICATED", 401), (Code(stale), stale.Error!.Code.Status));
        Assert.Equal("UNAUTHENTICATED", Code(await host.SignIn.BeginSwitchAsync(switched with { PersonId = Guid.NewGuid() }, Net, Ct)));
        Assert.Equal("UNAUTHENTICATED", Code(await host.SignIn.BeginSwitchAsync(switched with { MembershipId = inA.Id, TenantId = a.Id }, Net, Ct)));
        // 绝对寿命到期后不能再切换。
        host.Clock.Advance(TimeSpan.FromDays(31));
        Assert.Equal("UNAUTHENTICATED", Code(await host.SignIn.BeginSwitchAsync(switched, Net, Ct)));
    }

    [Fact]
    public async Task 租户处于pending_grace_suspended时新登录被拒_trial与active放行_选择期间被冻结则选择失败且票据保留()
    {
        await using var host = await AccountsHost.Start(postgres);
        var (person, phone) = await CreatePerson(host);
        foreach (var status in new[] { TenantStatus.Pending, TenantStatus.Grace, TenantStatus.Suspended })
            await Join(host, host.Tenants.Add("t" + status.ToString().ToLowerInvariant(), status), person.Id);
        var denied = await Right(host, phone);
        Assert.Equal(("TENANT_NOT_ACTIVE", 403), (Code(denied), denied.Error!.Code.Status));
        Assert.Equal("tenant_not_active", await host.Text("SELECT reason FROM plugin_qian_identity.login_audits"));
        Assert.Equal("0", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions"));

        var trial = host.Tenants.Add("trialco", TenantStatus.Trial);
        var trialMember = await Join(host, trial, person.Id);
        var direct = await Right(host, phone); // 其余三个不可登录，唯一可用的直接进入
        Assert.Equal((SignInStatus.Authenticated, trial.Id, trialMember.Id), (direct.Status, direct.Session!.TenantId, direct.Session.MembershipId));
        var active = host.Tenants.Add("liveco");
        var activeMember = await Join(host, active, person.Id);
        var choose = await Right(host, phone);
        Assert.Equal("liveco,trialco", string.Join(',', choose.Choices.Select(c => c.TenantCode!).Order()));
        // 选择期间租户被冻结：该选项被拒绝，票据保留，可改选另一个。
        host.Tenants.SetStatus(trial.Id, TenantStatus.Suspended);
        Assert.Equal("TENANT_NOT_ACTIVE", Code(await Pick(host, choose.Ticket!, trialMember.Id)));
        var fallback = await Pick(host, choose.Ticket!, activeMember.Id);
        Assert.Equal((SignInStatus.Authenticated, active.Id), (fallback.Status, fallback.Session!.TenantId));
        // 已建立的会话随租户状态变化失效；恢复后又可用（会话行本身没被改动）。
        Assert.NotNull(await host.Service.ValidateAsync(fallback.Session.SessionId, person.Id, activeMember.Id, Ct));
        host.Tenants.SetStatus(active.Id, TenantStatus.Grace);
        Assert.Null(await host.Service.ValidateAsync(fallback.Session.SessionId, person.Id, activeMember.Id, Ct));
        host.Tenants.SetStatus(active.Id, TenantStatus.Active);
        Assert.NotNull(await host.Service.ValidateAsync(fallback.Session.SessionId, person.Id, activeMember.Id, Ct));
        // 全部不可用 → 租户不可登录；没有任何成员 → 无可用身份。
        host.Tenants.SetStatus(active.Id, TenantStatus.Suspended);
        Assert.Equal("TENANT_NOT_ACTIVE", Code(await Right(host, phone)));
        var (_, lonerPhone) = await CreatePerson(host, "孤身一人");
        Assert.Equal("NO_AVAILABLE_TENANT", Code(await Right(host, lonerPhone)));
    }

    [Fact]
    public async Task 平台身份登录得到tenant_id为空的会话_平台事件在租户作用域外入队_没有平台资格的人没有平台选项_资格收回后会话失效()
    {
        await using var host = await AccountsHost.Start(postgres);
        var tenant = host.Tenants.Add("acme");
        var (operatorPerson, phone) = await CreatePerson(host, "运营", platform: true);
        // 只有平台身份：直接进入。
        var onlyPlatform = await Right(host, phone);
        Assert.Equal((SignInStatus.Authenticated, (Guid?)null, (Guid?)null), (onlyPlatform.Status, onlyPlatform.Session!.TenantId, onlyPlatform.Session.MembershipId));
        var platformLogin = Assert.Single(await Events(host, "identity.login.v1"));
        Assert.Null(platformLogin.TenantId);
        Assert.Equal("", await host.Text("SELECT coalesce(tenant_id, '') FROM plugin_qian_identity.outbox_messages WHERE event_type_id = 'identity.login.v1'"));
        Assert.Equal("", await host.Text("SELECT coalesce(membership_id::text, '') FROM plugin_qian_identity.sessions"));
        Assert.NotNull(await host.Service.ValidateAsync(onlyPlatform.Session.SessionId, operatorPerson.Id, null, Ct));
        // 同时是租户成员：需要选择，其中有一项是平台身份。
        var member = await Join(host, tenant, operatorPerson.Id);
        var both = await Right(host, phone);
        Assert.Equal(SignInStatus.SelectionRequired, both.Status);
        Assert.Equal(1, both.Choices.Count(c => c.IsPlatform));
        var platformChoice = await Pick(host, both.Ticket!, null);
        Assert.Equal((SignInStatus.Authenticated, (Guid?)null), (platformChoice.Status, platformChoice.Session!.TenantId));
        // 指定租户登录时不出现平台身份。
        var tenantOnly = await host.SignIn.SignInAsync(Input(phone, Password, tenant: tenant.Id), Net, Ct);
        Assert.Equal(member.Id, tenantOnly.Session!.MembershipId);
        // 没有平台资格的人没有平台选项。
        var (ordinary, ordinaryPhone) = await CreatePerson(host, "普通人");
        await Join(host, tenant, ordinary.Id); await Join(host, host.Tenants.Add("other"), ordinary.Id);
        Assert.DoesNotContain((await Right(host, ordinaryPhone)).Choices, c => c.IsPlatform);
        // 平台资格被收回之后，平台会话立即失效。
        await host.Exec("UPDATE plugin_qian_identity.persons SET platform_access = false WHERE id = @p::uuid", ("p", Id(operatorPerson.Id)));
        Assert.Null(await host.Service.ValidateAsync(platformChoice.Session.SessionId, operatorPerson.Id, null, Ct));
        Assert.Null(await host.Service.ValidateAsync(onlyPlatform.Session.SessionId, operatorPerson.Id, null, Ct));
    }

    [Fact]
    public async Task 已登记第二因子的账号一律拒绝_绝不降级成单因子_密码错误时不暴露第二因子状态()
    {
        await using var host = await AccountsHost.Start(postgres);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        await host.Exec("""UPDATE plugin_qian_identity.persons SET mfa_factors = '[{"type":"totp","enrolled_at":"2026-10-01T00:00:00Z","secret_ref":"secretRef://x"}]'::jsonb WHERE id = @p::uuid""", ("p", Id(person.Id)));
        var result = await Right(host, phone);
        Assert.Equal(("MFA_UNAVAILABLE", 503), (Code(result), result.Error!.Code.Status));
        Assert.Equal("0", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions"));
        Assert.Equal("mfa_unavailable", await host.Text("SELECT reason FROM plugin_qian_identity.login_audits"));
        // 即使宿主另外注册了别的因子，也不能让账号只凭密码通过。
        using var registration = host.Factors.Register(new CodeFactor("otp", "123456"));
        Assert.Equal("MFA_UNAVAILABLE", Code(await Right(host, phone)));
        // 密码不对时不暴露第二因子状态。
        Assert.Equal("INVALID_CREDENTIALS", Code(await Wrong(host, phone)));
    }

    [Fact]
    public async Task 其他认证因子经同一LoginInput走同一条限流_审计与签发路径_注册表同名独占且可撤销()
    {
        await using var host = await AccountsHost.Start(postgres, Policy(max: 3));
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        Assert.Equal("password", string.Join(',', host.Factors.List().Select(f => f.Id)));
        var conflict = Assert.Throws<QianErrorException>(() => host.Factors.Register(new CodeFactor("password", "x")));
        Assert.Equal("REGISTRATION_CONFLICT", conflict.Error.Code.Code);
        var registration = host.Factors.Register(new CodeFactor("otp", "123456"));
        Assert.Equal("otp,password", string.Join(',', host.Factors.List().Select(f => f.Id)));
        Assert.Throws<QianErrorException>(() => host.Factors.Register(new CodeFactor("otp", "other")));

        LoginInput Otp(string code) => new("web", null, null, "otp", Proof(code)) { Identifier = phone };
        var good = await host.SignIn.SignInAsync(Otp("123456"), Net, Ct);
        Assert.Equal((SignInStatus.Authenticated, "2"), (good.Status, good.Session!.Acr));
        Assert.Equal("otp", Assert.Single(good.Session.Amr));
        Assert.Equal("otp", await host.Text("SELECT method FROM plugin_qian_identity.login_audits WHERE outcome = 'success'"));
        Assert.Single(await Events(host, "identity.login.v1"));
        // 其他因子同样受错误次数限制：与密码共用同一个计数。
        Assert.Equal("INVALID_CREDENTIALS", Code(await host.SignIn.SignInAsync(Otp("000000"), Net, Ct)));
        Assert.Equal("INVALID_CREDENTIALS", Code(await Wrong(host, phone)));
        Assert.Equal("ACCOUNT_LOCKED", Code(await host.SignIn.SignInAsync(Otp("000000"), Net, Ct)));
        Assert.Equal("ACCOUNT_LOCKED", Code(await Right(host, phone)));
        Assert.Equal("ACCOUNT_LOCKED", Code(await host.SignIn.SignInAsync(Otp("123456"), Net, Ct)));
        // 撤销注册后该方式不存在（重复撤销无副作用）；内置的 password 不受影响。
        registration.Dispose(); registration.Dispose();
        var gone = await host.SignIn.SignInAsync(Otp("123456"), Net, Ct);
        Assert.Equal("#/factor_id", gone.Error!.Errors.Single().Pointer);
        Assert.Equal("password", string.Join(',', host.Factors.List().Select(f => f.Id)));
    }

    [Fact]
    public async Task 图形验证码按策略开关_缺失与错误是不同的结构化错误且不消耗密码尝试_挑战单次有效且两分钟过期()
    {
        await using var host = await AccountsHost.Start(postgres, Policy(max: 3, captcha: CaptchaMode.Always));
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        var missing = await Right(host, phone);
        Assert.Equal(("CAPTCHA_REQUIRED", 428), (Code(missing), missing.Error!.Code.Status));
        // 对不存在的账号一视同仁：验证码先于账号查找，不能借此判断账号是否存在。
        Assert.Equal("CAPTCHA_REQUIRED", Code(await host.SignIn.SignInAsync(Input("13900000009", "x"), Net, Ct)));

        async Task<(LoginInput Input, CaptchaChallenge Challenge)> Solved(bool correct = true, string? password = null)
        {
            var challenge = await host.Service.IssueCaptchaAsync(Ct);
            var answer = Solve(challenge.ImageSvg);
            Assert.Matches("^[A-HJKMNP-Z2-9]{5}$", answer);
            return (Input(phone, password ?? Password) with { Captcha = new CaptchaAnswer(Id(challenge.Id), correct ? answer : "AAAAA") }, challenge);
        }
        var wrong = await host.SignIn.SignInAsync((await Solved(correct: false)).Input, Net, Ct);
        Assert.Equal(("CAPTCHA_INVALID", 400), (Code(wrong), wrong.Error!.Code.Status));
        Assert.Equal("0", await PersonValue(host, person.Id, "failed_attempts::text")); // 验证码错误不消耗密码尝试
        var (solved, challenge) = await Solved();
        // 库里只有答案的加盐哈希：32 字节，且不是裸答案的哈希。
        var answerText = Solve(challenge.ImageSvg);
        Assert.Equal("32", await host.Text("SELECT length(answer_hash)::text FROM plugin_qian_identity.login_captchas WHERE id = @id::uuid", ("id", Id(challenge.Id))));
        Assert.Equal("0", await host.Text("SELECT count(*) FROM plugin_qian_identity.login_captchas WHERE id = @id::uuid AND answer_hash = sha256(convert_to(@a, 'UTF8'))", ("id", Id(challenge.Id)), ("a", answerText)));
        Assert.Equal(SignInStatus.Authenticated, (await host.SignIn.SignInAsync(solved, Net, Ct)).Status);
        // 挑战单次有效：同一个挑战与答案重放被拒。
        Assert.Equal("CAPTCHA_INVALID", Code(await host.SignIn.SignInAsync(solved, Net, Ct)));
        // 密码错误仍计入阈值；验证码对了不等于放行。
        for (var i = 0; i < 2; i++) Assert.Equal("INVALID_CREDENTIALS", Code(await host.SignIn.SignInAsync((await Solved(password: "Wrong-Pass-1")).Input, Net, Ct)));
        Assert.Equal("ACCOUNT_LOCKED", Code(await host.SignIn.SignInAsync((await Solved(password: "Wrong-Pass-1")).Input, Net, Ct)));
        // 两分钟过期。
        var expiring = (await Solved()).Input;
        host.Clock.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1));
        Assert.Equal("CAPTCHA_INVALID", Code(await host.SignIn.SignInAsync(expiring, Net, Ct)));
        // 格式错乱的应答直接视为无效。
        Assert.Equal("CAPTCHA_INVALID", Code(await host.SignIn.SignInAsync(Input(phone, Password) with { Captcha = new CaptchaAnswer("not-a-guid", "AAAAA") }, Net, Ct)));
        Assert.Equal("CAPTCHA_INVALID", Code(await host.SignIn.SignInAsync(Input(phone, Password) with { Captcha = new CaptchaAnswer(Id(Guid.NewGuid()), new string('A', 40)) }, Net, Ct)));
    }

    [Fact]
    public async Task 验证码策略为连续失败后才要求时_失败达到次数才出现_成功后清零不再要求()
    {
        await using var host = await AccountsHost.Start(postgres, Policy(max: 10, captcha: CaptchaMode.AfterFailures, captchaAfter: 2));
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        Assert.Equal("INVALID_CREDENTIALS", Code(await Wrong(host, phone)));
        Assert.Equal("INVALID_CREDENTIALS", Code(await Wrong(host, phone)));
        Assert.Equal("CAPTCHA_REQUIRED", Code(await Right(host, phone))); // 已经连错两次：正确密码也要先过验证码
        Assert.Equal("2", await PersonValue(host, person.Id, "failed_attempts::text")); // 缺验证码不消耗尝试
        var challenge = await host.Service.IssueCaptchaAsync(Ct);
        var solved = Input(phone, Password) with { Captcha = new CaptchaAnswer(Id(challenge.Id), Solve(challenge.ImageSvg)) };
        Assert.Equal(SignInStatus.Authenticated, (await host.SignIn.SignInAsync(solved, Net, Ct)).Status);
        Assert.Equal(SignInStatus.Authenticated, (await Right(host, phone)).Status); // 成功清零后不再要求
    }

    [Fact]
    public async Task 验证码关闭时不签发挑战_登录不要求验证码()
    {
        await using var host = await AccountsHost.Start(postgres);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        var failure = await Assert.ThrowsAsync<QianErrorException>(async () => await host.Service.IssueCaptchaAsync(Ct));
        Assert.Equal("NOT_FOUND", failure.Error.Code.Code);
        Assert.Equal(SignInStatus.Authenticated, (await Right(host, phone)).Status);
    }

    [Fact]
    public async Task 账号会话有效性对照数据库_吊销_过期_账号停用_成员停用_租户状态_修订取Person与成员之和()
    {
        await using var world = await TwoTenants(postgres);
        var (host, a, _, person, phone, inA, _) = world;
        var session = (await Pick(host, (await Right(host, phone)).Ticket!, inA.Id)).Session!;
        var current = await host.Service.ValidateAsync(session.SessionId, person.Id, inA.Id, Ct);
        Assert.Equal((a.Id, inA.Id, 0L), (current!.TenantId, current.MembershipId, current.IdentityRevision));
        // 参数与会话不一致一律无效。
        Assert.Null(await host.Service.ValidateAsync(session.SessionId, Guid.NewGuid(), inA.Id, Ct));
        Assert.Null(await host.Service.ValidateAsync(session.SessionId, person.Id, Guid.NewGuid(), Ct));
        Assert.Null(await host.Service.ValidateAsync(session.SessionId, person.Id, null, Ct));
        Assert.Null(await host.Service.ValidateAsync(Guid.NewGuid(), person.Id, inA.Id, Ct));
        // 纵深防御：失效来自账号与成员的当前状态，而不只是会话行——绕过 API 直接改库也立刻生效，改回即恢复。
        await host.Exec("UPDATE plugin_qian_identity.persons SET status = 'disabled' WHERE id = @p::uuid", ("p", Id(person.Id)));
        Assert.Null(await host.Service.ValidateAsync(session.SessionId, person.Id, inA.Id, Ct));
        await host.Exec("UPDATE plugin_qian_identity.persons SET status = 'active' WHERE id = @p::uuid", ("p", Id(person.Id)));
        Assert.NotNull(await host.Service.ValidateAsync(session.SessionId, person.Id, inA.Id, Ct));
        await host.Exec("UPDATE plugin_qian_identity.memberships SET status = 'disabled' WHERE id = @m::uuid", ("m", Id(inA.Id)));
        Assert.Null(await host.Service.ValidateAsync(session.SessionId, person.Id, inA.Id, Ct));
        await host.Exec("UPDATE plugin_qian_identity.memberships SET status = 'active' WHERE id = @m::uuid", ("m", Id(inA.Id)));
        Assert.NotNull(await host.Service.ValidateAsync(session.SessionId, person.Id, inA.Id, Ct));
        // 修订：Person 与成员各自递增后相加，调用方据此判断持有的修订是否过期；新登录的会话带最新值。
        await host.Exec("UPDATE plugin_qian_identity.persons SET identity_revision = identity_revision + 1 WHERE id = @p::uuid", ("p", Id(person.Id)));
        Assert.Equal(1, (await host.Service.ValidateAsync(session.SessionId, person.Id, inA.Id, Ct))!.IdentityRevision);
        await host.Exec("UPDATE plugin_qian_identity.memberships SET identity_revision = identity_revision + 2 WHERE id = @m::uuid", ("m", Id(inA.Id)));
        Assert.Equal(3, (await host.Service.ValidateAsync(session.SessionId, person.Id, inA.Id, Ct))!.IdentityRevision);
        var renewed = (await Pick(host, (await Right(host, phone)).Ticket!, inA.Id)).Session!;
        Assert.Equal(3, renewed.IdentityRevision);
        // 经 API 停用会吊销该账号的全部会话，重新启用不会复活旧会话。
        await host.Accounts.SetPersonStatusAsync(person.Id, AccountStatuses.Disabled, "ops", Ct);
        Assert.Null(await host.Service.ValidateAsync(renewed.SessionId, person.Id, inA.Id, Ct));
        await host.Accounts.SetPersonStatusAsync(person.Id, AccountStatuses.Active, "ops", Ct);
        Assert.Null(await host.Service.ValidateAsync(renewed.SessionId, person.Id, inA.Id, Ct));
        Assert.Equal("disabled", await host.Text("SELECT revoke_reason FROM plugin_qian_identity.sessions WHERE id = @sid::uuid", ("sid", Id(renewed.SessionId))));
        // 显式吊销：指定会话只吊销它；不指定则吊销该账号的全部会话。
        var one = (await Pick(host, (await Right(host, phone)).Ticket!, inA.Id)).Session!;
        var two = (await Pick(host, (await Right(host, phone)).Ticket!, inA.Id)).Session!;
        await host.Service.RevokeSessionsAsync(new SessionRevocation(person.Id, one.SessionId, "forced", "k1"), Ct);
        Assert.Null(await host.Service.ValidateAsync(one.SessionId, person.Id, inA.Id, Ct));
        Assert.NotNull(await host.Service.ValidateAsync(two.SessionId, person.Id, inA.Id, Ct));
        await host.Service.RevokeSessionsAsync(new SessionRevocation(person.Id, null, "forced", "k2"), Ct);
        Assert.Null(await host.Service.ValidateAsync(two.SessionId, person.Id, inA.Id, Ct));
        // 绝对寿命到期。
        var expiring = (await Pick(host, (await Right(host, phone)).Ticket!, inA.Id)).Session!;
        host.Clock.Advance(TimeSpan.FromDays(30) + TimeSpan.FromSeconds(1));
        Assert.Null(await host.Service.ValidateAsync(expiring.SessionId, person.Id, inA.Id, Ct));
    }

    [Fact]
    public async Task 端类型进入会话与审计_请求可覆盖端类型但格式非法时忽略_客户端镜像记录类型()
    {
        await using var host = await AccountsHost.Start(postgres);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        Assert.Equal("mobile", (await host.SignIn.SignInAsync(Input(phone, Password, "mobile"), Net, Ct)).Session!.DeviceType);
        Assert.Equal("pc", (await Right(host, phone)).Session!.DeviceType);
        Assert.Equal("agent", (await host.SignIn.SignInAsync(Input(phone, Password), Net with { DeviceType = "agent" }, Ct)).Session!.DeviceType);
        Assert.Equal("pc", (await host.SignIn.SignInAsync(Input(phone, Password), Net with { DeviceType = "PC; DROP TABLE" }, Ct)).Session!.DeviceType);
        Assert.Equal("4", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions WHERE person_id = @p::uuid", ("p", Id(person.Id))));
        Assert.Equal("1", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions WHERE client_id = 'mobile' AND device_type = 'mobile'"));
        Assert.Equal("1", await host.Text("SELECT count(*) FROM plugin_qian_identity.clients WHERE client_id = 'mobile' AND device_type = 'mobile' AND type = 'public' AND status = 'active'"));
        Assert.Equal("machine", await host.Text("SELECT type FROM plugin_qian_identity.clients WHERE client_id = 'machine'"));
        Assert.Equal("confidential", await host.Text("SELECT type FROM plugin_qian_identity.clients WHERE client_id = 'web'"));
    }

    // 从 SVG 里按出现顺序取出答案字符：验证码的图形噪声只挡脚本，测试用它当「人眼」。
    private static string Solve(string svg) => string.Concat(TextNodes().Matches(svg).Select(match => match.Groups[1].Value));

    [GeneratedRegex("<text [^>]*>([A-Z0-9])</text>", RegexOptions.CultureInvariant)]
    private static partial Regex TextNodes();

    // 一个固定验证码的第二因子：用来证明其他认证方式走的是同一条流水线。
    private sealed class CodeFactor(string id, string code) : IAuthenticatorFactor
    {
        public FactorDescriptor Descriptor { get; } = new(id, "测试验证码", "otp", false);

        public Task<AuthenticationChallenge?> BeginAsync(AuthenticationFactorContext context, CancellationToken cancellationToken = default) => Task.FromResult<AuthenticationChallenge?>(null);

        public Task<AuthenticationResult> VerifyAsync(AuthenticationProof proof, AuthenticationFactorContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(Encoding.UTF8.GetString(proof.Data.Span) == code
                ? new AuthenticationResult(true, null, ["otp"], "2")
                : new AuthenticationResult(false, new QianError(IdentityErrors.InvalidCredentials), [], null));
    }
}
