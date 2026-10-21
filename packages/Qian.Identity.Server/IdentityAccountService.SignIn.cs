using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors;
using Qian.Tenancy;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Qian.Identity.Server;

// 统一登录流水线：所有登录方式构造同一个 LoginInput，经同一条「限流 → 因子验证 → 身份选择 → 会话签发 → 审计」路径。
internal sealed partial class IdentityAccountService
{
    internal const string PasswordFactorId = "password";
    private const string PasswordAmr = "pwd";
    private const string Persons = IdentitySql.Persons;
    private const string Tickets = IdentitySql.Tickets;
    private const string SelectionPurpose = "selection";
    private const string PasswordChangePurpose = "password_change";

    private static readonly Regex DeviceTypePattern = new(@"\A[a-z][a-z0-9_-]{0,15}\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private readonly FactorRegistry _factors = new();

    // ---- 审计 ----

    // 一次流水线调用对应的审计骨架；标识只留类型与掩码，不留原值。
    private sealed record AuditSubject(string Stage, string? ClientId, string? Method, string? IdentifierKind, string? IdentifierMasked, LoginContext Context);

    private static string? Truncate(string? value, int max) => value is null ? null : value.Length <= max ? value : value[..max];

    private static LoginAuditRow NewAudit(AuditSubject subject, string outcome, string? reason, DateTimeOffset now,
        Guid? personId = null, Guid? membershipId = null, Guid? tenantId = null, Guid? sid = null) => new()
    {
        Id = Guid.CreateVersion7(now), OccurredAt = now, Stage = subject.Stage, Outcome = outcome, Reason = reason, PersonId = personId, MembershipId = membershipId,
        TenantId = tenantId?.ToString("D"), IdentifierKind = subject.IdentifierKind, IdentifierMasked = subject.IdentifierMasked,
        ClientId = Truncate(subject.ClientId, 100), Method = Truncate(subject.Method, 64), RemoteAddress = Truncate(subject.Context.RemoteAddress, 64),
        UserAgent = Truncate(subject.Context.UserAgent, 256), SessionHash = sid is null ? null : SidHash(sid.Value), TraceId = Activity.Current?.TraceId.ToString()
    };

    private async Task WriteAuditAsync(LoginAuditRow row, CancellationToken ct)
    {
        using var neutral = TenantScope(null);
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        session.Context.LoginAudits.Add(row);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // 失败也写且仅写一条审计；返回给调用方的错误不区分账号不存在与密码错误。
    private async ValueTask<SignInResult> RejectAsync(AuditSubject subject, QianError error, string reason, Guid? personId, CancellationToken ct, TimeSpan? retryAfter = null)
    {
        await WriteAuditAsync(NewAudit(subject, "failure", reason, Now, personId), ct).ConfigureAwait(false);
        return SignInResult.Failure(error, retryAfter);
    }

    private static QianError Field(ErrorCode code, string pointer) => new(PlatformErrors.InputInvalid) { Errors = [new QianFieldError(code, pointer)] };

    // ---- 登录 ----

    public async ValueTask<SignInResult> SignInAsync(LoginInput input, LoginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        context ??= new LoginContext();
        var (kind, masked) = IdentifierRules.MaskInput(input.Identifier, _account.DefaultPhoneCountryCode);
        var subject = new AuditSubject("authenticate", input.ClientId, input.ConnectorId is null ? input.FactorId ?? PasswordFactorId : "connector:" + Truncate(input.ConnectorId, 48), kind, masked, context);
        // 连接器登录由 S1-IDP-007 接入同一条路径；在此之前明确拒绝，而不是悄悄降级成别的方式。
        if (input.ConnectorId is not null)
            return await RejectAsync(subject, Field(PlatformErrors.ValueNotAllowed, "#/connector_id"), "method_unsupported", null, cancellationToken).ConfigureAwait(false);
        var factor = _factors.Find(input.FactorId ?? PasswordFactorId);
        if (factor is null)
            return await RejectAsync(subject, Field(PlatformErrors.ValueNotAllowed, "#/factor_id"), "method_unsupported", null, cancellationToken).ConfigureAwait(false);
        if (FindClient(input.ClientId) is not { } client)
            return await RejectAsync(subject, Field(PlatformErrors.ValueNotAllowed, "#/client_id"), "client_unknown", null, cancellationToken).ConfigureAwait(false);
        if (input.Proof is null)
            return await RejectAsync(subject, Field(PlatformErrors.Required, "#/proof"), "invalid_request", null, cancellationToken).ConfigureAwait(false);
        var login = _account.Login;
        // 「每次都要求」的验证码对存在与不存在的账号一视同仁，先于账号查找校验。
        if (login.Captcha == CaptchaMode.Always && await CheckCaptchaAsync(input, subject, null, cancellationToken).ConfigureAwait(false) is { } captchaFailure) return captchaFailure;
        var classified = IdentifierRules.Classify(input.Identifier, _account.DefaultPhoneCountryCode);
        var person = classified is null ? null : await FindForLoginAsync(classified.Value.Kind, classified.Value.Value, cancellationToken).ConfigureAwait(false);
        if (person is null)
        {
            // 账号不存在也做一次等价成本的哈希，响应时间不泄露账号是否存在。
            if (factor.Descriptor.Id == PasswordFactorId) { using var secret = PasswordSecret.From(input.Proof); _hasher.BurnDummy(secret.Bytes); }
            return await RejectAsync(subject, new QianError(IdentityErrors.InvalidCredentials), "unknown_account", null, cancellationToken).ConfigureAwait(false);
        }
        var now = Now;
        if (login.Captcha == CaptchaMode.AfterFailures && RecentFailures(person, now) >= login.CaptchaAfterFailures &&
            await CheckCaptchaAsync(input, subject, person.Id, cancellationToken).ConfigureAwait(false) is { } afterFailures) return afterFailures;
        // 先占用一次尝试再验证：并发猜测不会在计数落库前各自验证一遍。
        var correlation = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        var outcome = await VerifyWithThrottleAsync(person.Id, async token =>
        {
            var factorContext = new AuthenticationFactorContext(person.Id, client.Application.ClientId!, input.TenantId, correlation);
            return await factor.VerifyAsync(input.Proof, factorContext, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        if (outcome.Locked)
            return await RejectAsync(subject, new QianError(IdentityErrors.AccountLocked), outcome.JustLocked ? "locked_now" : "locked", person.Id, cancellationToken, outcome.RetryAfter).ConfigureAwait(false);
        if (outcome.Result is not { Succeeded: true } verified)
            return await RejectAsync(subject, outcome.Result?.Error ?? new QianError(IdentityErrors.InvalidCredentials), "bad_credentials", person.Id, cancellationToken).ConfigureAwait(false);
        // 凭据正确之后才暴露账号状态。
        if (person.Status != AccountStatuses.Active)
            return await RejectAsync(subject, new QianError(IdentityErrors.AccountDisabled), "account_" + person.Status, person.Id, cancellationToken).ConfigureAwait(false);
        // 已登记第二因子而本实例没有可用的验证实现时一律拒绝，绝不降级成单因子。
        if (HasEnrolledFactors(person) && !_factors.HasSecondFactors)
            return await RejectAsync(subject, new QianError(IdentityErrors.MfaUnavailable), "mfa_unavailable", person.Id, cancellationToken).ConfigureAwait(false);
        var amr = verified.Amr.Count == 0 ? [PasswordAmr] : verified.Amr.ToArray();
        var state = new AuthState(person.Id, client.Application.ClientId!, DeviceTypeOf(client, context), amr, verified.Acr, now);
        if (person.MustChangePassword)
            return await IssueAsync(subject, state, PasswordChangePurpose, input.TenantId, [], null, null, cancellationToken).ConfigureAwait(false);
        return await ContinueAsync(subject, state, input.TenantId, null, null, false, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<SignInResult> SelectAsync(TenantSelection selection, LoginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        context ??= new LoginContext();
        var subject = new AuditSubject("select", null, null, null, null, context);
        var hash = TicketHash(selection.Ticket);
        if (hash is null) return await RejectAsync(subject, new QianError(IdentityErrors.LoginTicketInvalid), "ticket_invalid", null, cancellationToken).ConfigureAwait(false);
        var ticket = await PeekTicketAsync(hash, SelectionPurpose, cancellationToken).ConfigureAwait(false);
        if (ticket is null) return await RejectAsync(subject, new QianError(IdentityErrors.LoginTicketInvalid), "ticket_invalid", null, cancellationToken).ConfigureAwait(false);
        subject = subject with { ClientId = ticket.ClientId, Method = ticket.FromSession is null ? "selection" : "switch" };
        // 只能在签发时确认过的集合内选；集合外的成员 ID 既可能是篡改，也可能属于别的账号。
        var index = ticket.Choices.ToList().FindIndex(c => c.Membership == selection.MembershipId);
        if (index < 0) return await RejectAsync(subject, Field(PlatformErrors.ValueNotAllowed, "#/membership_id"), "choice_unknown", ticket.PersonId, cancellationToken).ConfigureAwait(false);
        var state = new AuthState(ticket.PersonId, ticket.ClientId, ticket.DeviceType, ticket.Amr, ticket.Acr, ticket.AuthenticatedAt);
        return await EstablishAsync(subject with { Stage = ticket.FromSession is null ? "select" : "switch" }, state, ticket.Choices[index], hash, SelectionPurpose, ticket.FromSession, null, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<SignInResult> ChangeInitialPasswordAsync(InitialPasswordChange change, LoginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        context ??= new LoginContext();
        var subject = new AuditSubject("change_password", null, PasswordFactorId, null, null, context);
        var hash = TicketHash(change.Ticket);
        var ticket = hash is null ? null : await PeekTicketAsync(hash, PasswordChangePurpose, cancellationToken).ConfigureAwait(false);
        if (hash is null || ticket is null) return await RejectAsync(subject, new QianError(IdentityErrors.LoginTicketInvalid), "ticket_invalid", null, cancellationToken).ConfigureAwait(false);
        subject = subject with { ClientId = ticket.ClientId };
        using var next = PasswordSecret.From(change.NewPassword);
        try { CheckPassword(next); }
        catch (QianErrorException policy) { return await RejectAsync(subject, policy.Error, "password_policy", ticket.PersonId, cancellationToken).ConfigureAwait(false); }
        var now = Now;
        try
        {
            using var neutral = TenantScope(null); using var origin = OpenOrigin(ticket.PersonId);
            await using var session = await OpenAsync(cancellationToken).ConfigureAwait(false);
            var db = session.Context;
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            // 改密与票据消费同事务：票据只能换一次密码，且改密失败不会烧掉票据。
            if (!await ConsumeTicketAsync(db, hash, PasswordChangePurpose, now, cancellationToken).ConfigureAwait(false)) throw Error(IdentityErrors.LoginTicketInvalid);
            var row = await LockPersonAsync(db, ticket.PersonId, cancellationToken).ConfigureAwait(false);
            if (row.Status != AccountStatuses.Active) throw Error(IdentityErrors.AccountDisabled);
            await EnsureNotReusedAsync(db, row, next, cancellationToken).ConfigureAwait(false);
            await SetPasswordAsync(db, row, next, false, now, cancellationToken).ConfigureAwait(false);
            row.IdentityRevision++;
            await RevokeSessionRowsAsync(db, row.Id, null, null, "password_changed", now, cancellationToken).ConfigureAwait(false);
            await SaveAsync(session, row, new IdentityPasswordChangedEvent(row.Id, row.IdentityRevision), cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (QianErrorException failure)
        {
            return await RejectAsync(subject, failure.Error, failure.Error.Code == IdentityErrors.PasswordReused ? "password_reused" : "change_rejected", ticket.PersonId, cancellationToken).ConfigureAwait(false);
        }
        // 改密之后继续同一登录流程（选择租户或直接建立会话）；认证时刻与方式沿用第一因子。
        var state = new AuthState(ticket.PersonId, ticket.ClientId, ticket.DeviceType, ticket.Amr, ticket.Acr, ticket.AuthenticatedAt);
        return await ContinueAsync(subject, state, ticket.TenantFilter, null, "initial_password_changed", false, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<SignInResult> BeginSwitchAsync(AccountSession current, LoginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(current);
        context ??= new LoginContext();
        var subject = new AuditSubject("switch", current.ClientId, "switch", null, null, context);
        // 切换只是换身份，不提升认证强度：当前会话必须仍然有效。
        var validation = await ValidateAsync(current.SessionId, current.PersonId, current.MembershipId, cancellationToken).ConfigureAwait(false);
        if (validation is null) return await RejectAsync(subject, new QianError(PlatformErrors.Unauthenticated), "session_invalid", current.PersonId, cancellationToken).ConfigureAwait(false);
        var state = new AuthState(current.PersonId, current.ClientId, current.DeviceType, current.Amr, current.Acr, current.AuthenticatedAt);
        return await ContinueAsync(subject, state, null, current.SessionId, "switch_begin", true, cancellationToken).ConfigureAwait(false);
    }

    // ---- 限流：占位在先，验证在后 ----

    private sealed record Throttle(bool Locked, bool JustLocked, TimeSpan? RetryAfter, AuthenticationResult? Result);

    // 在行锁内占用一次尝试并决定是否放行；验证在锁外进行，所以慢哈希不会拖住其他请求。
    // 成功清零，失败保留占位；达到阈值那次尝试起账号进入锁定（或累进延时）。
    private async Task<Throttle> VerifyWithThrottleAsync(Guid personId, Func<CancellationToken, Task<AuthenticationResult>> verify, CancellationToken ct)
    {
        var gate = await ReserveAttemptAsync(personId, ct).ConfigureAwait(false);
        if (gate.Blocked) return new Throttle(true, false, gate.RetryAfter, null);
        var result = await verify(ct).ConfigureAwait(false);
        if (result.Succeeded)
        {
            await ClearFailuresAsync(personId, ct).ConfigureAwait(false);
            return new Throttle(false, false, null, result);
        }
        return gate.JustLocked ? new Throttle(true, true, gate.RetryAfter, result) : new Throttle(false, false, null, result);
    }

    private sealed record Gate(bool Blocked, bool JustLocked, TimeSpan? RetryAfter);

    private async Task<Gate> ReserveAttemptAsync(Guid personId, CancellationToken ct)
    {
        var now = Now; var policy = _account.Login;
        using var neutral = TenantScope(null); using var origin = OpenOrigin();
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        var db = session.Context;
        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var row = await LockPersonAsync(db, personId, ct).ConfigureAwait(false);
        // 管理员置为 locked 的账号只能由管理员解锁，不看计数。
        if (row.Status == AccountStatuses.Locked) return new Gate(true, false, null);
        if (row.LockoutUntil is { } until && until > now) return new Gate(true, false, until - now);
        var failed = (row.LastFailedAt is { } last && now - last <= policy.FailureWindow ? row.FailedAttempts : 0) + 1;
        row.FailedAttempts = failed; row.LastFailedAt = now;
        var span = policy.LockoutFor(failed);
        row.LockoutUntil = span is { } lockFor ? now + lockFor : null;
        // 只在越过阈值的那一次发锁定事件，后续的延时累加不重复通知。
        var crossed = span is not null && failed == policy.MaxFailedAttempts;
        await SaveAsync(session, row, crossed ? new IdentityLockedEvent(row.Id, row.IdentityRevision, "too_many_attempts", row.LockoutUntil) : null, ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return new Gate(false, span is not null, span);
    }

    private async Task ClearFailuresAsync(Guid personId, CancellationToken ct)
    {
        using var neutral = TenantScope(null);
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        await IdentitySql.ExecuteAsync(session.Context,
            $"UPDATE {Persons} SET failed_attempts = 0, last_failed_at = NULL, lockout_until = NULL WHERE id = @id AND (failed_attempts <> 0 OR lockout_until IS NOT NULL)",
            ct, ("id", personId)).ConfigureAwait(false);
    }

    private int RecentFailures(PersonRow person, DateTimeOffset now) =>
        person.LastFailedAt is { } last && now - last <= _account.Login.FailureWindow ? person.FailedAttempts : 0;

    // ---- 因子 ----

    private async Task<bool> VerifyStoredPasswordAsync(Guid personId, PasswordSecret secret, CancellationToken ct)
    {
        string? hash;
        {
            using var neutral = TenantScope(null);
            await using var session = await OpenAsync(ct).ConfigureAwait(false);
            hash = await session.Context.Persons.AsNoTracking().Where(p => p.Id == personId).Select(p => p.PasswordHash).SingleOrDefaultAsync(ct).ConfigureAwait(false);
        }
        if (hash is null) { _hasher.BurnDummy(secret.Bytes); return false; }
        if (!Argon2idPasswordHasher.Verify(hash, secret.Bytes)) return false;
        // 成本参数调高之后，旧哈希在下一次成功登录时顺带升级；比较并交换，不覆盖并发的改密。
        if (_hasher.NeedsRehash(hash))
        {
            var upgraded = _hasher.Hash(secret.Bytes);
            using var neutral = TenantScope(null);
            await using var session = await OpenAsync(ct).ConfigureAwait(false);
            await IdentitySql.ExecuteAsync(session.Context, $"UPDATE {Persons} SET password_hash = @upgraded WHERE id = @id AND password_hash = @old", ct,
                ("upgraded", upgraded), ("id", personId), ("old", hash)).ConfigureAwait(false);
        }
        return true;
    }

    private sealed class PasswordFactor(IdentityAccountService owner) : IAuthenticatorFactor
    {
        public FactorDescriptor Descriptor { get; } = new(PasswordFactorId, "密码", PasswordAmr, false);

        public Task<AuthenticationChallenge?> BeginAsync(AuthenticationFactorContext context, CancellationToken cancellationToken = default) => Task.FromResult<AuthenticationChallenge?>(null);

        public async Task<AuthenticationResult> VerifyAsync(AuthenticationProof proof, AuthenticationFactorContext context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(proof); ArgumentNullException.ThrowIfNull(context);
            using var secret = PasswordSecret.From(proof);
            return await owner.VerifyStoredPasswordAsync(context.PersonId, secret, cancellationToken).ConfigureAwait(false)
                ? new AuthenticationResult(true, null, [PasswordAmr], null)
                : new AuthenticationResult(false, new QianError(IdentityErrors.InvalidCredentials), [], null);
        }
    }

    private sealed class FactorRegistry : IAuthenticatorFactorRegistry
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<string, IAuthenticatorFactor> _items = new(StringComparer.Ordinal);

        // 注册第一个非密码因子之前，已登记第二因子的账号一律拒绝登录（fail-closed）。
        internal bool HasSecondFactors { get { lock (_gate) return _items.Keys.Any(id => id != PasswordFactorId); } }

        internal void Seed(IAuthenticatorFactor factor) { lock (_gate) _items[factor.Descriptor.Id] = factor; }

        public IDisposable Register(IAuthenticatorFactor factor)
        {
            ArgumentNullException.ThrowIfNull(factor);
            var id = factor.Descriptor.Id;
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            lock (_gate)
            {
                if (!_items.TryAdd(id, factor))
                    throw Error(PlatformErrors.RegistrationConflict, new Dictionary<string, string> { ["key"] = id, ["owner"] = "qian.identity" });
            }
            return new Handle(this, id, factor);
        }

        public IAuthenticatorFactor? Find(string id) { lock (_gate) return _items.GetValueOrDefault(id); }

        public IReadOnlyList<FactorDescriptor> List() { lock (_gate) return _items.Values.Select(f => f.Descriptor).OrderBy(d => d.Id, StringComparer.Ordinal).ToArray(); }

        private sealed class Handle(FactorRegistry owner, string id, IAuthenticatorFactor factor) : IDisposable
        {
            private int _disposed;
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
                lock (owner._gate) { if (owner._items.TryGetValue(id, out var current) && ReferenceEquals(current, factor)) owner._items.Remove(id); }
            }
        }
    }

    private static bool HasEnrolledFactors(PersonRow person) => person.MfaFactors.Trim() is not ("" or "[]" or "null");

    // ---- 账号与客户端查找 ----

    private IdentityClientRegistration? FindClient(string? clientId) => string.IsNullOrWhiteSpace(clientId) ? null :
        _options.Clients.SingleOrDefault(c => c.Application.ClientId == clientId && c.Application.Permissions.Contains(Permissions.Endpoints.Authorization));

    private static string DeviceTypeOf(IdentityClientRegistration client, LoginContext context) =>
        context.DeviceType is { } requested && DeviceTypePattern.IsMatch(requested) ? requested : client.DeviceType;

    private async Task<PersonRow?> FindForLoginAsync(IdentifierKind kind, string value, CancellationToken ct)
    {
        using var neutral = TenantScope(null);
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        var name = IdentifierRules.KindName(kind);
        var person = await (from identifier in session.Context.Identifiers.AsNoTracking()
                            join row in session.Context.Persons.AsNoTracking() on identifier.PersonId equals row.Id
                            where identifier.Kind == name && identifier.Value == value && row.Status != AccountStatuses.Purged
                            select row).SingleOrDefaultAsync(ct).ConfigureAwait(false);
        return person;
    }

    // ---- 身份选择与会话建立 ----

    private sealed record AuthState(Guid PersonId, string ClientId, string DeviceType, IReadOnlyList<string> Amr, string? Acr, DateTimeOffset AuthenticatedAt);

    private sealed record Resolution(IReadOnlyList<TenantChoice> Choices, bool Blocked);

    // 列出此刻可登录的身份：租户必须 trial / active，成员必须 active；具备平台资格的人另有平台身份。
    // Membership 是租户 RLS 表，所以逐个租户进入作用域读取，从不绕过；租户集合取自 Person 上的登录索引。
    private async Task<Resolution> ResolveChoicesAsync(Guid personId, Guid? tenantFilter, CancellationToken ct)
    {
        PersonRow person;
        {
            using var neutral = TenantScope(null);
            await using var session = await OpenAsync(ct).ConfigureAwait(false);
            person = await session.Context.Persons.AsNoTracking().SingleAsync(p => p.Id == personId, ct).ConfigureAwait(false);
        }
        var choices = new List<TenantChoice>(); var blocked = false;
        foreach (var raw in person.TenantIds)
        {
            if (!Guid.TryParseExact(raw, "D", out var tenantId) || (tenantFilter is { } only && only != tenantId)) continue;
            using var scope = TenantScope(tenantId);
            await using var session = await OpenAsync(ct).ConfigureAwait(false);
            var members = await session.Context.Memberships.AsNoTracking().Where(m => m.PersonId == personId && m.Status == AccountStatuses.Active).ToListAsync(ct).ConfigureAwait(false);
            if (members.Count == 0) continue;
            var tenant = await Directory.GetByIdAsync(tenantId, ct).ConfigureAwait(false);
            if (tenant is null || tenant.Status is not (TenantStatus.Active or TenantStatus.Trial)) { blocked = true; continue; }
            foreach (var member in members)
                choices.Add(new TenantChoice(member.Id, tenantId, tenant.Code, tenant.Name, member.DisplayName, false, member.IsTenantAdministrator));
        }
        if (person.PlatformAccess && tenantFilter is null) choices.Add(new TenantChoice(null, null, null, null, person.DisplayName, true, false));
        return new Resolution(choices, blocked);
    }

    // 第一因子（以及强制改密）通过之后的统一出口：一个身份就直接建立会话，多个身份发 5 分钟选择令牌。
    private async ValueTask<SignInResult> ContinueAsync(AuditSubject subject, AuthState state, Guid? tenantFilter, Guid? switchFrom, string? reason, bool alwaysSelect, CancellationToken ct)
    {
        var resolution = await ResolveChoicesAsync(state.PersonId, tenantFilter, ct).ConfigureAwait(false);
        if (resolution.Choices.Count == 0)
            return await RejectAsync(subject, new QianError(resolution.Blocked ? IdentityErrors.TenantNotActive : IdentityErrors.NoAvailableTenant),
                resolution.Blocked ? "tenant_not_active" : "no_membership", state.PersonId, ct).ConfigureAwait(false);
        if (resolution.Choices.Count == 1 && !alwaysSelect)
        {
            var only = resolution.Choices[0];
            return await EstablishAsync(subject, state, (only.MembershipId, only.TenantId), null, null, null, reason, ct).ConfigureAwait(false);
        }
        return await IssueAsync(subject, state, SelectionPurpose, tenantFilter, resolution.Choices, switchFrom, reason, ct).ConfigureAwait(false);
    }

    private static string ChoiceKey(TenantChoice choice) => choice.IsPlatform ? "p" : $"m:{choice.MembershipId:D}:{choice.TenantId:D}";

    // 签发一次性票据并写同一事务的审计；库里只存令牌的 SHA-256。
    private async ValueTask<SignInResult> IssueAsync(AuditSubject subject, AuthState state, string purpose, Guid? tenantFilter, IReadOnlyList<TenantChoice> choices,
        Guid? fromSession, string? reason, CancellationToken ct)
    {
        var now = Now; var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var lifetime = purpose == SelectionPurpose ? _account.Login.SelectionLifetime : _account.Login.ChangeTicketLifetime;
        var expires = now + lifetime;
        using var neutral = TenantScope(null);
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        session.Context.LoginTickets.Add(new LoginTicketRow
        {
            TokenHash = SHA256.HashData(Encoding.ASCII.GetBytes(token)), Purpose = purpose, PersonId = state.PersonId, ClientId = state.ClientId, DeviceType = state.DeviceType,
            TenantId = tenantFilter?.ToString("D"), Choices = [.. choices.Select(ChoiceKey)], FromSessionId = fromSession, Amr = [.. state.Amr], Acr = state.Acr,
            AuthenticatedAt = state.AuthenticatedAt, RemoteAddress = Truncate(subject.Context.RemoteAddress, 64), UserAgent = Truncate(subject.Context.UserAgent, 256),
            CreatedAt = now, ExpiresAt = expires
        });
        session.Context.LoginAudits.Add(NewAudit(subject with { ClientId = state.ClientId }, purpose == SelectionPurpose ? "selection_required" : "password_change_required", reason, now, state.PersonId));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        var ticket = new LoginTicket(purpose == SelectionPurpose ? LoginTicketPurpose.Selection : LoginTicketPurpose.PasswordChange, token, expires);
        return purpose == SelectionPurpose ? SignInResult.SelectionRequired(ticket, choices) : SignInResult.PasswordChangeRequired(ticket);
    }

    // 建立账号会话。所有写入（票据消费、会话、审计、登录事件、切换时的旧会话吊销）在同一事务、
    // 目标租户的作用域内完成：事件按所属租户入 Outbox，任何一步失败都整体回滚，票据不会被烧掉。
    private async ValueTask<SignInResult> EstablishAsync(AuditSubject subject, AuthState state, (Guid? Membership, Guid? Tenant) pick, byte[]? ticketHash, string? ticketPurpose,
        Guid? replaceSession, string? reason, CancellationToken ct)
    {
        var now = Now;
        subject = subject with { ClientId = state.ClientId };
        try
        {
            using var scope = TenantScope(pick.Tenant); using var origin = OpenOrigin(state.PersonId);
            await using var session = await OpenAsync(ct).ConfigureAwait(false);
            var db = session.Context;
            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            if (ticketHash is not null && !await ConsumeTicketAsync(db, ticketHash, ticketPurpose!, now, ct).ConfigureAwait(false)) throw Error(IdentityErrors.LoginTicketInvalid);
            var person = await LockPersonAsync(db, state.PersonId, ct).ConfigureAwait(false);
            if (person.Status != AccountStatuses.Active) throw Error(IdentityErrors.AccountDisabled);
            MembershipRow? member = null;
            if (pick.Membership is { } membershipId)
            {
                member = await db.Memberships.AsNoTracking().SingleOrDefaultAsync(m => m.Id == membershipId && m.PersonId == person.Id, ct).ConfigureAwait(false);
                if (member is null || member.Status != AccountStatuses.Active) throw Error(IdentityErrors.NoAvailableTenant);
                var tenant = await Directory.GetByIdAsync(pick.Tenant!.Value, ct).ConfigureAwait(false);
                if (tenant is null || tenant.Status is not (TenantStatus.Active or TenantStatus.Trial)) throw Error(IdentityErrors.TenantNotActive);
            }
            else if (!person.PlatformAccess) throw Error(IdentityErrors.NoAvailableTenant);
            // 令牌里的 identity_revision 是 Person 修订与成员修订之和：任何一侧变化都使旧令牌失效。
            var revision = person.IdentityRevision + (member?.IdentityRevision ?? 0);
            var sid = Guid.NewGuid();
            // 绝对寿命从最初认证时刻算起：切换租户不会延长登录。
            var expires = state.AuthenticatedAt + _account.Session.AbsoluteLifetime;
            if (expires <= now) throw Error(IdentityErrors.LoginTicketInvalid);
            db.Sessions.Add(new SessionRow
            {
                Id = sid, PersonId = person.Id, MembershipId = member?.Id, TenantId = pick.Tenant?.ToString("D"), ClientId = state.ClientId, DeviceType = state.DeviceType,
                Amr = [.. state.Amr], Acr = state.Acr, IdentityRevision = revision, RemoteAddress = Truncate(subject.Context.RemoteAddress, 64), UserAgent = Truncate(subject.Context.UserAgent, 256),
                CreatedAt = now, AuthenticatedAt = state.AuthenticatedAt, LastSeenAt = now, AbsoluteExpiresAt = expires
            });
            person.LastLoginAt = now;
            if (replaceSession is { } old) await RevokeSessionRowsAsync(db, person.Id, old, null, "switch", now, ct).ConfigureAwait(false);
            db.LoginAudits.Add(NewAudit(subject, "success", reason, now, person.Id, member?.Id, pick.Tenant, sid));
            await SaveAsync(session, person, new IdentityLoginEvent(person.Id, revision, sid, state.ClientId, [.. state.Amr], member?.Id), ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return SignInResult.Authenticated(new AccountSession(person.Id, pick.Tenant, member?.Id, sid, revision, [.. state.Amr], state.Acr, state.AuthenticatedAt, expires, state.ClientId, state.DeviceType));
        }
        catch (QianErrorException failure)
        {
            var code = failure.Error.Code;
            return await RejectAsync(subject, failure.Error, code == IdentityErrors.LoginTicketInvalid ? "ticket_invalid" : code == IdentityErrors.TenantNotActive ? "tenant_not_active" : code == IdentityErrors.AccountDisabled ? "account_disabled" : "no_membership",
                state.PersonId, ct).ConfigureAwait(false);
        }
    }

    // ---- 票据 ----

    private sealed record TicketState(string ClientId, string DeviceType, Guid PersonId, Guid? TenantFilter, IReadOnlyList<(Guid? Membership, Guid? Tenant)> Choices,
        Guid? FromSession, IReadOnlyList<string> Amr, string? Acr, DateTimeOffset AuthenticatedAt);

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // 令牌是 43 个 base64url 字符；格式不对的输入不进数据库。
    private static byte[]? TicketHash(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length is < 32 or > 64) return null;
        foreach (var c in token) if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')) return null;
        return SHA256.HashData(Encoding.ASCII.GetBytes(token));
    }

    private async Task<TicketState?> PeekTicketAsync(byte[] hash, string purpose, CancellationToken ct)
    {
        var now = Now;
        using var neutral = TenantScope(null);
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        var row = await session.Context.LoginTickets.AsNoTracking()
            .SingleOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == purpose && t.ConsumedAt == null && t.ExpiresAt > now, ct).ConfigureAwait(false);
        if (row is null) return null;
        var choices = new List<(Guid? Membership, Guid? Tenant)>();
        foreach (var item in row.Choices)
        {
            if (item == "p") { choices.Add((null, null)); continue; }
            var parts = item.Split(':');
            if (parts.Length == 3 && parts[0] == "m" && Guid.TryParseExact(parts[1], "D", out var membership) && Guid.TryParseExact(parts[2], "D", out var tenant)) choices.Add((membership, tenant));
        }
        return new TicketState(row.ClientId, row.DeviceType, row.PersonId, Guid.TryParseExact(row.TenantId, "D", out var filter) ? filter : null, choices, row.FromSessionId,
            row.Amr, row.Acr, row.AuthenticatedAt);
    }

    private static async Task<bool> ConsumeTicketAsync(IdentityDbContext db, byte[] hash, string purpose, DateTimeOffset now, CancellationToken ct) =>
        await IdentitySql.ExecuteAsync(db, $"UPDATE {Tickets} SET consumed_at = @now WHERE token_hash = @hash AND purpose = @purpose AND consumed_at IS NULL AND expires_at > @now", ct,
            ("now", now), ("hash", hash), ("purpose", purpose)).ConfigureAwait(false) == 1;

    // ---- 会话有效性（IAccountSessionValidator） ----

    public async Task<AccountValidation?> ValidateAsync(Guid sessionId, Guid personId, Guid? membershipId, CancellationToken ct)
    {
        var now = Now;
        SessionRow? stored; PersonRow? person;
        {
            using var neutral = TenantScope(null);
            await using var session = await OpenAsync(ct).ConfigureAwait(false);
            stored = await session.Context.Sessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sessionId && s.PersonId == personId && s.RevokedAt == null && s.AbsoluteExpiresAt > now, ct).ConfigureAwait(false);
            person = stored is null ? null : await session.Context.Persons.AsNoTracking().SingleOrDefaultAsync(p => p.Id == personId, ct).ConfigureAwait(false);
        }
        if (stored is null || person is not { Status: AccountStatuses.Active } || stored.MembershipId != membershipId) return null;
        if (stored.MembershipId is not { } storedMembership)
            return person.PlatformAccess ? new AccountValidation(person.IdentityRevision, null, null) : null;
        if (!Guid.TryParseExact(stored.TenantId, "D", out var tenantId)) return null;
        using var scope = TenantScope(tenantId);
        await using var tenantSession = await OpenAsync(ct).ConfigureAwait(false);
        var member = await tenantSession.Context.Memberships.AsNoTracking().SingleOrDefaultAsync(m => m.Id == storedMembership && m.PersonId == personId && m.Status == AccountStatuses.Active, ct).ConfigureAwait(false);
        if (member is null) return null;
        var tenant = await Directory.GetByIdAsync(tenantId, ct).ConfigureAwait(false);
        return tenant is { Status: TenantStatus.Active or TenantStatus.Trial } ? new AccountValidation(person.IdentityRevision + member.IdentityRevision, tenantId, member.Id) : null;
    }
}
