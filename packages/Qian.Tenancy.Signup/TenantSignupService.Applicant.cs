using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors;

namespace Qian.Tenancy.Signup;

// 申请人一侧（规格 §5.1、§6）：发码与核验、申请人会话、租户号可用性、本人的申请、提交与撤回。
internal sealed partial class TenantSignupService
{
    public async ValueTask<SignupVerificationTicket> StartVerificationAsync(StartSignupVerification request, SignupRequestContext context, CancellationToken cancellationToken = default)
    {
        Check(); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(context);
        var phone = SignupRules.NormalizePhone(request.Phone, _options.DefaultCountryCode) ?? throw SignupRules.Invalid(PlatformErrors.PatternMismatch, "#/phone");
        // 没有发件端口就发不出验证码：自助开通不可用。不查号码是否已注册，响应与时间都不因此不同。
        var messenger = Messenger ?? throw Error(TenancySignupErrors.Unavailable);
        var source = Source(context);
        var now = _clock.GetUtcNow();
        var code = SignupRules.NewCode(_options.CodeLength);
        var salt = RandomNumberGenerator.GetBytes(16);
        var row = new SignupVerificationRow { Phone = phone, CodeSalt = salt, CodeHash = SignupRules.HashCode(salt, code), ExpiresAt = now + _options.CodeLifetime, Source = source, CreatedAt = now };
        await using (var session = await OpenAsync(cancellationToken).ConfigureAwait(false))
        {
            var db = session.Context;
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            // 先锁手机号再锁来源（固定顺序不成环）：同一号码或同一来源的并发请求逐个判定，不会一起穿过阈值。
            await XactLockAsync(db, "phone:" + phone, cancellationToken).ConfigureAwait(false);
            await XactLockAsync(db, "source:" + source, cancellationToken).ConfigureAwait(false);
            var hour = now.AddHours(-1);
            var byPhone = await db.Verifications.AsNoTracking().Where(v => v.Phone == phone && v.CreatedAt > hour).OrderBy(v => v.CreatedAt).Select(v => v.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
            if (byPhone.Count > 0 && byPhone[^1] + _options.ResendInterval > now) throw RateLimited(byPhone[^1] + _options.ResendInterval - now);
            if (byPhone.Count >= _options.PerPhonePerHour) throw RateLimited(byPhone[byPhone.Count - _options.PerPhonePerHour].AddHours(1) - now);
            var bySource = await db.Verifications.AsNoTracking().Where(v => v.Source == source && v.CreatedAt > hour).OrderBy(v => v.CreatedAt).Select(v => v.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);
            if (bySource.Count >= _options.PerSourcePerHour) throw RateLimited(bySource[bySource.Count - _options.PerSourcePerHour].AddHours(1) - now);
            // 限流只看最近一小时：一天前的挑战顺带清掉。
            var cutoff = now.AddDays(-1);
            await db.Verifications.Where(v => v.CreatedAt < cutoff).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            db.Verifications.Add(row);
            await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        try { await messenger.SendVerificationCodeAsync(new SignupVerificationMessage(row.Id, phone, code, row.ExpiresAt), cancellationToken).ConfigureAwait(false); }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            LogSendFailed(_context.Logger, row.Id, error);
            await using (var session = await OpenAsync(CancellationToken.None).ConfigureAwait(false))
                await session.Context.Verifications.Where(v => v.Id == row.Id).ExecuteUpdateAsync(v => v.SetProperty(x => x.ConsumedAt, now), CancellationToken.None).ConfigureAwait(false);
            throw Error(TenancySignupErrors.Unavailable);
        }
        return new SignupVerificationTicket(row.Id, row.ExpiresAt, now + _options.ResendInterval);
    }

    public async ValueTask<SignupSessionGrant> OpenSessionAsync(OpenSignupSession request, SignupRequestContext context, CancellationToken cancellationToken = default)
    {
        Check(); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrEmpty(request.Code)) throw SignupRules.Invalid(PlatformErrors.Required, "#/code");
        if (request.Code.Length > 16) throw SignupRules.Invalid(PlatformErrors.OutOfRange, "#/code");
        var now = _clock.GetUtcNow();
        string phone;
        await using (var session = await OpenAsync(cancellationToken).ConfigureAwait(false))
        {
            var db = session.Context;
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await XactLockAsync(db, "verification:" + request.VerificationId.ToString("D"), cancellationToken).ConfigureAwait(false);
            var row = await db.Verifications.SingleOrDefaultAsync(v => v.Id == request.VerificationId, cancellationToken).ConfigureAwait(false);
            // 不存在、已用过、已作废、已过期一律同一个错误：只能重新获取。
            if (row is null || row.ConsumedAt is not null || row.ExpiresAt <= now || row.Attempts >= _options.MaxAttempts) throw Error(TenancySignupErrors.CodeExpired);
            if (!SignupRules.CodeMatches(row.CodeSalt, row.CodeHash, request.Code))
            {
                row.Attempts++;
                if (row.Attempts >= _options.MaxAttempts) row.ConsumedAt = now;
                await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                throw new QianErrorException(new QianError(TenancySignupErrors.CodeInvalid, new Dictionary<string, string> { ["remaining"] = Text(_options.MaxAttempts - row.Attempts) }));
            }
            row.ConsumedAt = now;
            await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            phone = row.Phone;
        }
        // 核验通过之后才看号码是否已有账号：已有且可申请时以该自然人申请，停用、锁定或平台身份拒绝。
        var person = await Accounts.FindPersonAsync(phone, cancellationToken).ConfigureAwait(false);
        if (person is not null && !Eligible(person)) throw Error(TenancySignupErrors.AccountUnavailable);
        var (token, expires) = await IssueSessionAsync(phone, person?.Id, null, cancellationToken).ConfigureAwait(false);
        return new SignupSessionGrant(token, expires, Applicant(phone, person));
    }

    public async ValueTask<SignupApplicant?> GetApplicantAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        Check();
        return await ResolveAsync(sessionToken, cancellationToken).ConfigureAwait(false) is { } resolved ? Applicant(resolved.Session.Phone, resolved.Person) : null;
    }

    public async ValueTask CloseSessionAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        Check();
        if (SignupRules.HashToken(sessionToken) is not { } hash) return;
        var now = _clock.GetUtcNow();
        await using var session = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await session.Context.Sessions.Where(s => s.TokenHash == hash && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<SignupCodeAvailability> CheckCodeAsync(string code, SignupRequestContext context, CancellationToken cancellationToken = default)
    {
        Check(); ArgumentNullException.ThrowIfNull(code); ArgumentNullException.ThrowIfNull(context);
        using var lease = _codeChecks.AttemptAcquire(Source(context));
        if (!lease.IsAcquired)
            throw RateLimited(lease.TryGetMetadata(System.Threading.RateLimiting.MetadataName.RetryAfter, out var retry) ? retry : TimeSpan.FromMinutes(1));
        if (SignupRules.CodeProblem(code, _options) is { } problem) return new SignupCodeAvailability(code, false) { Reason = problem };
        return await TakenAsync(code, null, cancellationToken).ConfigureAwait(false)
            ? new SignupCodeAvailability(code, false) { Reason = SignupCodeReasons.Taken } : new SignupCodeAvailability(code, true);
    }

    public async ValueTask<SignupApplicantApplications> ListMineAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        Check();
        var resolved = await ResolveAsync(sessionToken, cancellationToken).ConfigureAwait(false) ?? throw Error(PlatformErrors.Unauthenticated);
        if (resolved.Person is not { } person) return new SignupApplicantApplications([]);
        await using var session = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await session.Context.Applications.AsNoTracking().Where(a => a.PersonId == person.Id)
            .OrderByDescending(a => a.SubmittedAt).ThenByDescending(a => a.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new SignupApplicantApplications(Array.AsReadOnly(rows.Select(ApplicantView).ToArray()));
    }

    public async ValueTask<SignupSubmission> SubmitAsync(string sessionToken, SubmitSignupApplication request, CancellationToken cancellationToken = default)
    {
        Check(); ArgumentNullException.ThrowIfNull(request);
        var resolved = await ResolveAsync(sessionToken, cancellationToken).ConfigureAwait(false) ?? throw Error(PlatformErrors.Unauthenticated);
        var input = ValidateSubmission(request);
        var existing = resolved.Person;
        if (existing is not null)
        {
            // 以既有账号申请：不建新账号、不改密码，姓名与密码都不得出现。
            if (!Eligible(existing)) throw Error(TenancySignupErrors.AccountUnavailable);
            if (request.DisplayName is not null) throw SignupRules.Invalid(PlatformErrors.ValueNotAllowed, "#/displayName");
            if (request.Password is not null) throw SignupRules.Invalid(PlatformErrors.ValueNotAllowed, "#/password");
        }
        else
        {
            if (request.DisplayName is null) throw SignupRules.Invalid(PlatformErrors.Required, "#/displayName");
            if (!SignupRules.IsName(request.DisplayName, 200)) throw SignupRules.Invalid(PlatformErrors.PatternMismatch, "#/displayName");
            if (string.IsNullOrEmpty(request.Password)) throw SignupRules.Invalid(PlatformErrors.Required, "#/password");
        }
        // 提交全局串行：先判定进行中的申请与租户号，再建账号，避免判定通过、账号已建、申请却因并发冲突没建成。
        return await LockedAsync("submit", async () =>
        {
            if (existing is not null && await HasOpenAsync(existing.Id, cancellationToken).ConfigureAwait(false)) throw Error(TenancySignupErrors.ApplicationOpen);
            if (await TakenAsync(input.Code, null, cancellationToken).ConfigureAwait(false)) throw CodeTaken();
            var person = existing ?? await CreatePersonAsync(resolved.Session.Phone, request.DisplayName!.Trim(), request.Password!, cancellationToken).ConfigureAwait(false);
            var now = _clock.GetUtcNow();
            SignupApplicationRow row;
            await using (var session = await OpenAsync(cancellationToken).ConfigureAwait(false))
            {
                var db = session.Context;
                var prefix = SignupRules.Number(now, 0)[..12];
                var sequence = await db.Applications.CountAsync(a => a.Number.StartsWith(prefix), cancellationToken).ConfigureAwait(false) + 1;
                row = new SignupApplicationRow
                {
                    Number = SignupRules.Number(now, sequence), PersonId = person.Id, CompanyName = input.CompanyName, Code = input.Code, Industry = input.Industry, Size = request.Size,
                    Description = input.Description, ContactName = person.DisplayName, Phone = resolved.Session.Phone, Email = input.Email, ExistingAccount = existing is not null, SubmittedAt = now
                };
                db.Applications.Add(row);
                try { await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false); }
                catch (DbUpdateException error) when (IsUnique(error, "ux_applications_open_code")) { throw CodeTaken(); }
                catch (DbUpdateException error) when (IsUnique(error, "ux_applications_open_person")) { throw Error(TenancySignupErrors.ApplicationOpen); }
            }
            // 提交后轮换申请人会话：新会话绑定到申请人的自然人，旧令牌作废。
            var (token, expires) = await IssueSessionAsync(resolved.Session.Phone, person.Id, resolved.Session.Id, cancellationToken).ConfigureAwait(false);
            return new SignupSubmission(ApplicantView(row), new SignupSessionGrant(token, expires, Applicant(resolved.Session.Phone, person)));
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<SignupApplicantApplication> WithdrawAsync(string sessionToken, Guid id, CancellationToken cancellationToken = default)
    {
        Check();
        var resolved = await ResolveAsync(sessionToken, cancellationToken).ConfigureAwait(false) ?? throw Error(PlatformErrors.Unauthenticated);
        if (resolved.Person is not { } person) throw Error(PlatformErrors.NotFound);
        return await LockedAsync("application:" + id.ToString("D"), async () =>
        {
            await using var session = await OpenAsync(cancellationToken).ConfigureAwait(false);
            // 不是本人的申请与不存在同为 404，不泄露别人的申请是否存在。
            var row = await session.Context.Applications.SingleOrDefaultAsync(a => a.Id == id && a.PersonId == person.Id, cancellationToken).ConfigureAwait(false)
                ?? throw Error(PlatformErrors.NotFound);
            if (row.Status != SignupStatuses.Submitted) throw Error(TenancySignupErrors.StateConflict);
            row.Status = SignupStatuses.Withdrawn; row.CompletedAt = _clock.GetUtcNow(); row.Version++;
            try { await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false); }
            catch (DbUpdateConcurrencyException) { throw Error(TenancySignupErrors.StateConflict); }
            return ApplicantView(row);
        }, cancellationToken).ConfigureAwait(false);
    }

    private sealed record Submission(string CompanyName, string Code, string? Industry, string? Description, string? Email);

    private Submission ValidateSubmission(SubmitSignupApplication request)
    {
        if (request.CompanyName is null) throw SignupRules.Invalid(PlatformErrors.Required, "#/companyName");
        var company = request.CompanyName.Trim();
        if (!SignupRules.IsName(company, 200)) throw SignupRules.Invalid(PlatformErrors.PatternMismatch, "#/companyName");
        if (request.Code is null) throw SignupRules.Invalid(PlatformErrors.Required, "#/code");
        switch (SignupRules.CodeProblem(request.Code, _options))
        {
            case SignupCodeReasons.Invalid: throw SignupRules.Invalid(PlatformErrors.PatternMismatch, "#/code");
            case SignupCodeReasons.Reserved: throw SignupRules.Invalid(PlatformErrors.ValueNotAllowed, "#/code");
        }
        if (request.Size is null || !SignupCompanySizes.All.Contains(request.Size, StringComparer.Ordinal)) throw SignupRules.Invalid(PlatformErrors.ValueNotAllowed, "#/size");
        if (!request.AcceptTerms) throw SignupRules.Invalid(PlatformErrors.ValueNotAllowed, "#/acceptTerms");
        string? email = null;
        if (request.Email is not null && (email = SignupRules.NormalizeEmail(request.Email)) is null) throw SignupRules.Invalid(PlatformErrors.PatternMismatch, "#/email");
        var industry = string.IsNullOrWhiteSpace(request.Industry) ? null : request.Industry.Trim();
        if (industry is not null && !SignupRules.IsName(industry, 64)) throw SignupRules.Invalid(PlatformErrors.PatternMismatch, "#/industry");
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (!SignupRules.IsText(description, 2000)) throw SignupRules.Invalid(PlatformErrors.PatternMismatch, "#/description");
        return new Submission(company, request.Code, industry, description, email);
    }

    private async Task<Person> CreatePersonAsync(string phone, string displayName, string password, CancellationToken ct)
    {
        // 手机号刚经验证码核验，标记已验证；申请人自设密码、不强制改密；没有任何成员身份，也没有平台访问资格。
        using var proof = new AuthenticationProof(Encoding.UTF8.GetBytes(password));
        try
        {
            return await Accounts.CreatePersonAsync(new CreatePerson(displayName, [new IdentifierInput(IdentifierKind.Phone, phone, true)])
            { InitialPassword = proof, RequirePasswordChange = false, PlatformAccess = false }, ct).ConfigureAwait(false);
        }
        catch (QianErrorException error) { throw Remap(error); }
    }

    private async Task<bool> HasOpenAsync(Guid personId, CancellationToken ct)
    {
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        return await session.Context.Applications.AnyAsync(a => a.PersonId == personId && SignupStatuses.Open.Contains(a.Status), ct).ConfigureAwait(false);
    }

    // 租户号被占用：已有租户，或另一条进行中的申请预留了它。
    private async Task<bool> TakenAsync(string code, Guid? except, CancellationToken ct)
    {
        if (await Directory.GetAsync(code, ct).ConfigureAwait(false) is not null) return true;
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        return await session.Context.Applications.AnyAsync(a => a.Code == code && SignupStatuses.Open.Contains(a.Status) && a.Id != except, ct).ConfigureAwait(false);
    }

    private static QianErrorException CodeTaken() => new(new QianError(TenancySignupErrors.CodeTaken) { Pointer = "#/code" });

    private sealed record Resolved(SignupSessionRow Session, Person? Person);

    // 有效会话（未过期、未吊销）及其自然人：会话绑定的自然人以手机号重新解析，号码已不属于该自然人时会话作废。
    private async Task<Resolved?> ResolveAsync(string? token, CancellationToken ct)
    {
        if (SignupRules.HashToken(token) is not { } hash) return null;
        var now = _clock.GetUtcNow();
        SignupSessionRow? row;
        await using (var session = await OpenAsync(ct).ConfigureAwait(false))
            row = await session.Context.Sessions.AsNoTracking().SingleOrDefaultAsync(s => s.TokenHash == hash && s.RevokedAt == null && s.ExpiresAt > now, ct).ConfigureAwait(false);
        if (row is null) return null;
        var person = await Accounts.FindPersonAsync(row.Phone, ct).ConfigureAwait(false);
        if (row.PersonId is { } bound && person?.Id != bound) return null;
        return new Resolved(row, person);
    }

    private async Task<(string Token, DateTimeOffset ExpiresAt)> IssueSessionAsync(string phone, Guid? personId, Guid? replaces, CancellationToken ct)
    {
        var token = SignupRules.NewToken();
        var now = _clock.GetUtcNow();
        var row = new SignupSessionRow { TokenHash = SignupRules.HashToken(token)!, Phone = phone, PersonId = personId, ExpiresAt = now + _options.SessionLifetime, CreatedAt = now };
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        var db = session.Context;
        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        if (replaces is { } previous) await db.Sessions.Where(s => s.Id == previous && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct).ConfigureAwait(false);
        // 过期一天以上的会话顺带清掉。
        var cutoff = now.AddDays(-1);
        await db.Sessions.Where(s => s.ExpiresAt < cutoff).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        db.Sessions.Add(row);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return (token, row.ExpiresAt);
    }

    private static SignupApplicant Applicant(string phone, Person? person) => new(SignupRules.MaskPhone(phone), person is not null) { DisplayName = person?.DisplayName };

    // 申请人看到的申请：开通失败显示为开通中、不暴露原因；审核备注不出现；驳回原因只在已驳回时出现。
    private static SignupApplicantApplication ApplicantView(SignupApplicationRow row)
    {
        var status = SignupStatuses.Parse(row.Status);
        var approved = status == SignupApplicationStatus.Approved;
        return new SignupApplicantApplication(row.Id, row.Number, status == SignupApplicationStatus.Failed ? SignupApplicationStatus.Provisioning : status,
            row.CompanyName, row.Code, row.Size, row.ContactName, SignupRules.MaskPhone(row.Phone), row.SubmittedAt)
        {
            Industry = row.Industry, Description = row.Description, Email = row.Email, CompletedAt = row.CompletedAt,
            Plan = approved ? Plan(row.Plan) : null, ValidUntil = approved ? row.ValidUntil : null,
            RejectReason = status == SignupApplicationStatus.Rejected ? row.RejectReason : null
        };
    }

    private static SignupPlan? Plan(string? plan) => plan switch { "trial" => SignupPlan.Trial, "formal" => SignupPlan.Formal, _ => null };
}
