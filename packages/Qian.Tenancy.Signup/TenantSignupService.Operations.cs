using Microsoft.EntityFrameworkCore;
using Qian.Events;
using Qian.Runtime.Errors;

namespace Qian.Tenancy.Signup;

// 平台运营一侧（规格 §5.2）：列表与计数、详情与自动检查、通过 / 驳回 / 重试。调用方（宿主端点）已完成平台授权与旁路审计。
internal sealed partial class TenantSignupService
{
    // 开通编排里决定「申请人能直接登录新租户」的三步：管理员成员、管理员指针、tenant.owner。跳过（如身份组件缺席）也算没完成。
    private static readonly string[] RequiredSteps = ["05:membership", "06:administrator", "07:owner"];

    public async ValueTask<SignupApplicationList> QueryAsync(SignupApplicationQuery query, CancellationToken cancellationToken = default)
    {
        Check(); ArgumentNullException.ThrowIfNull(query);
        if (query.Skip < 0) throw SignupRules.Invalid(PlatformErrors.OutOfRange, "#/skip");
        if (query.Take is < 1 or > 100) throw SignupRules.Invalid(PlatformErrors.OutOfRange, "#/take");
        if (query.Status is { } wanted && !Enum.IsDefined(wanted)) throw SignupRules.Invalid(PlatformErrors.ValueNotAllowed, "#/status");
        if (query.Size is not null && !SignupCompanySizes.All.Contains(query.Size, StringComparer.Ordinal)) throw SignupRules.Invalid(PlatformErrors.ValueNotAllowed, "#/size");
        if (query.Keyword?.Length > 200) throw SignupRules.Invalid(PlatformErrors.OutOfRange, "#/keyword");
        SignupApplicationRow[] rows; int total; Dictionary<string, int> counts;
        await using (var session = await OpenAsync(cancellationToken).ConfigureAwait(false))
        {
            var db = session.Context;
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
            // 计数只受关键字与规模筛选影响，页签上的数字不随当前页签变化。
            var filtered = db.Applications.AsNoTracking();
            if (query.Size is { } size) filtered = filtered.Where(a => a.Size == size);
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var escaped = query.Keyword.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
                var pattern = "%" + escaped + "%";
                filtered = filtered.Where(a => EF.Functions.ILike(a.Number, pattern, "\\") || EF.Functions.ILike(a.CompanyName, pattern, "\\") ||
                    EF.Functions.ILike(a.Code, pattern, "\\") || EF.Functions.ILike(a.ContactName, pattern, "\\"));
            }
            counts = await filtered.GroupBy(a => a.Status).Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(item => item.Status, item => item.Count, StringComparer.Ordinal, cancellationToken).ConfigureAwait(false);
            var page = filtered;
            if (query.Status is { } status) { var name = SignupStatuses.Name(status); page = page.Where(a => a.Status == name); }
            total = await page.CountAsync(cancellationToken).ConfigureAwait(false);
            rows = await page.OrderByDescending(a => a.SubmittedAt).ThenByDescending(a => a.Id).Skip(query.Skip).Take(query.Take).ToArrayAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        var items = new List<SignupApplicationSummary>(rows.Length);
        foreach (var row in rows)
        {
            var checks = await ChecksAsync(row, cancellationToken).ConfigureAwait(false);
            items.Add(new SignupApplicationSummary(row.Id, row.Number, SignupStatuses.Parse(row.Status), row.CompanyName, row.Code, row.Size, row.ContactName,
                SignupRules.MaskPhone(row.Phone), row.SubmittedAt, MostSevere(checks)) { Industry = row.Industry });
        }
        int Count(string status) => counts.TryGetValue(status, out var value) ? value : 0;
        return new SignupApplicationList(items.AsReadOnly(), total, new SignupApplicationCounts(counts.Values.Sum(), Count(SignupStatuses.Submitted), Count(SignupStatuses.Provisioning),
            Count(SignupStatuses.Approved), Count(SignupStatuses.Failed), Count(SignupStatuses.Rejected), Count(SignupStatuses.Withdrawn)));
    }

    public async ValueTask<SignupApplicationDetail> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Check();
        return await DetailAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<SignupApplicationDetail> ApproveAsync(Guid id, ApproveSignupApplication request, string actor, CancellationToken cancellationToken = default)
    {
        Check(); ArgumentNullException.ThrowIfNull(request); ValidateActor(actor);
        if (!Enum.IsDefined(request.Plan)) throw SignupRules.Invalid(PlatformErrors.ValueNotAllowed, "#/plan");
        if (request.Code is not null)
            switch (SignupRules.CodeProblem(request.Code, _options))
            {
                case SignupCodeReasons.Invalid: throw SignupRules.Invalid(PlatformErrors.PatternMismatch, "#/code");
                case SignupCodeReasons.Reserved: throw SignupRules.Invalid(PlatformErrors.ValueNotAllowed, "#/code");
            }
        if (request.ValidUntil is { } until && until <= _clock.GetUtcNow()) throw SignupRules.Invalid(PlatformErrors.OutOfRange, "#/validUntil");
        if (request.Users is < 1 or > 100000) throw SignupRules.Invalid(PlatformErrors.OutOfRange, "#/users");
        if (!SignupRules.IsText(request.Note, 2000)) throw SignupRules.Invalid(PlatformErrors.PatternMismatch, "#/note");
        return await LockedAsync("application:" + id.ToString("D"), async () =>
        {
            SignupApplicationRow row;
            await using (var session = await OpenAsync(cancellationToken).ConfigureAwait(false))
            {
                row = await session.Context.Applications.SingleOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound);
                if (row.Status != SignupStatuses.Submitted) throw Error(TenancySignupErrors.StateConflict);
                // 租户号可在通过前修正；无论改没改，都要确认此刻没有被已有租户或其它申请占用，否则开通编排会撞号。
                var code = request.Code ?? row.Code;
                if (await TakenAsync(code, row.Id, cancellationToken).ConfigureAwait(false)) throw CodeTaken();
                var now = _clock.GetUtcNow();
                var trial = request.Plan == SignupPlan.Trial;
                row.Code = code; row.Status = SignupStatuses.Provisioning; row.Plan = trial ? "trial" : "formal";
                row.ValidUntil = request.ValidUntil?.ToUniversalTime() ?? (trial ? now.AddDays(_options.TrialDays) : null);
                row.Users = request.Users ?? _options.DefaultUsers; row.ReviewNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
                row.Reviewer = actor; row.ReviewedAt = now;
                // 管理员成员 ID 在申请上只生成一次：开通编排以它为幂等指针，重试沿用，不会建第二个成员。
                row.MembershipId ??= Guid.CreateVersion7();
                row.Attempts++; row.FailureReason = null; row.Version++;
                try { await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false); }
                catch (DbUpdateException error) when (IsUnique(error, "ux_applications_open_code")) { throw CodeTaken(); }
            }
            await RunAsync(row, actor).ConfigureAwait(false);
            return await DetailAsync(id, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<SignupApplicationDetail> RejectAsync(Guid id, RejectSignupApplication request, string actor, CancellationToken cancellationToken = default)
    {
        Check(); ArgumentNullException.ThrowIfNull(request); ValidateActor(actor);
        if (request.Reason is null) throw SignupRules.Invalid(PlatformErrors.Required, "#/reason");
        var reason = request.Reason.Trim();
        if (reason.Length == 0 || !SignupRules.IsText(reason, 500)) throw SignupRules.Invalid(reason.Length == 0 ? PlatformErrors.Required : PlatformErrors.PatternMismatch, "#/reason");
        if (!SignupRules.IsText(request.Note, 2000)) throw SignupRules.Invalid(PlatformErrors.PatternMismatch, "#/note");
        SignupApplicationRow row = null!;
        var detail = await LockedAsync("application:" + id.ToString("D"), async () =>
        {
            await using (var session = await OpenAsync(cancellationToken).ConfigureAwait(false))
            {
                row = await session.Context.Applications.SingleOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound);
                if (row.Status != SignupStatuses.Submitted) throw Error(TenancySignupErrors.StateConflict);
                var now = _clock.GetUtcNow();
                // 驳回即释放租户号（部分唯一索引只覆盖进行中的申请）；原因对申请人可见，备注只给运营看。
                row.Status = SignupStatuses.Rejected; row.RejectReason = reason; row.ReviewNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
                row.Reviewer = actor; row.ReviewedAt = now; row.CompletedAt = now; row.Version++;
                try { await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false); }
                catch (DbUpdateConcurrencyException) { throw Error(TenancySignupErrors.StateConflict); }
            }
            return await DetailAsync(id, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        await NotifyAsync(row, SignupResultKind.Rejected).ConfigureAwait(false);
        return detail;
    }

    public async ValueTask<SignupApplicationDetail> RetryAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        Check(); ValidateActor(actor);
        return await LockedAsync("application:" + id.ToString("D"), async () =>
        {
            SignupApplicationRow row;
            await using (var session = await OpenAsync(cancellationToken).ConfigureAwait(false))
            {
                row = await session.Context.Applications.SingleOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound);
                // 持有申请锁才走到这里：仍是 provisioning 说明上一次执行者已不在（进程中断），与 failed 一样可以续跑。
                if (row.Status is not (SignupStatuses.Failed or SignupStatuses.Provisioning) || row.MembershipId is null) throw Error(TenancySignupErrors.StateConflict);
                row.Status = SignupStatuses.Provisioning; row.Attempts++; row.Version++;
                await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            await RunAsync(row, actor).ConfigureAwait(false);
            return await DetailAsync(id, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    // 开通与激活：同一份输入（租户号、名称、管理员成员 ID 与自然人、配额）每次原样交给开通编排，编排按租户号幂等、只续跑没成功的步骤；
    // 激活到同一目标是空操作。中途失败时申请转 failed 并记原因码，运营重试后收敛到 approved。一旦开始就不随请求取消中断。
    private async Task RunAsync(SignupApplicationRow row, string actor)
    {
        var ct = CancellationToken.None;
        string? failure = null; Guid? tenantId = row.ProvisionedTenantId; Exception? cause = null;
        try
        {
            // 开通第 04 步要求事件信封的行为人就是 actor（人类运营）；产生方记本组件。
            using var origin = EventEnvelopeScope.Begin(new EventOrigin(TenancySignupDefinition.ComponentId, EventActor.User(actor)));
            var administrator = new TenantAdministrator(row.MembershipId!.Value, row.PersonId.ToString("D"));
            var tenant = await Provisioning.ProvisionAsync(new CreateTenant(row.Code, row.CompanyName) { Administrator = administrator, Quotas = new TenantQuotas(row.Users) },
                administrator, actor, ct).ConfigureAwait(false);
            tenantId = tenant.Id;
            var steps = await Provisioning.GetStepsAsync(tenant.Id, ct).ConfigureAwait(false);
            foreach (var name in RequiredSteps)
            {
                var step = steps.SingleOrDefault(item => item.Step == name);
                if (step is not { Status: "succeeded" }) throw new StepIncompleteException(step?.Reason ?? "step_incomplete");
            }
            await Lifecycle.ActivateAsync(tenant.Id, new ActivateTenant(row.Plan == "trial", row.ValidUntil), actor, ct).ConfigureAwait(false);
        }
        catch (Exception error)
        {
            // 只记稳定原因码，不记异常消息：Qian 错误码、编排步骤的原因码，其余一律 step_failed（与开通编排的步骤表同口径）。
            failure = error switch { QianErrorException qian => qian.Error.Code.Code, StepIncompleteException step => step.Reason, _ => "step_failed" };
            cause = error;
            // 编排在第 01 步之后失败时没有返回租户：按租户号找回它建的那个租户（CONFLICT 表示同号租户不是这份输入建的，不认领）。
            if (tenantId is null && failure != PlatformErrors.Conflict.Code)
            {
                try { tenantId = (await Directory.GetAsync(row.Code, ct).ConfigureAwait(false))?.Id; }
                catch (QianErrorException) { }
            }
        }
        await using (var session = await OpenAsync(ct).ConfigureAwait(false))
        {
            var current = await session.Context.Applications.SingleAsync(a => a.Id == row.Id, ct).ConfigureAwait(false);
            current.ProvisionedTenantId = tenantId; current.Version++;
            if (failure is null) { current.Status = SignupStatuses.Approved; current.FailureReason = null; current.CompletedAt = _clock.GetUtcNow(); }
            else { current.Status = SignupStatuses.Failed; current.FailureReason = failure; }
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
            row = current;
        }
        if (failure is not null) LogProvisionFailed(_context.Logger, row.Id, failure, cause!);
        else await NotifyAsync(row, SignupResultKind.Approved).ConfigureAwait(false);
    }

    // 审核结果通知：发件端口缺席或发送失败都不阻断审核，申请人仍可在进度页查看（与开通编排第 08 步 notify_unavailable 同口径）。
    private async Task NotifyAsync(SignupApplicationRow row, SignupResultKind kind)
    {
        if (Messenger is not { } messenger) return;
        try
        {
            await messenger.SendResultAsync(new SignupResultMessage(row.Id, row.Phone, row.Number, kind)
            {
                TenantCode = kind == SignupResultKind.Approved ? row.Code : null, ValidUntil = kind == SignupResultKind.Approved ? row.ValidUntil : null,
                Reason = kind == SignupResultKind.Rejected ? row.RejectReason : null
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception error) { LogNotifyFailed(_context.Logger, row.Id, kind, error); }
    }

    private async Task<SignupApplicationDetail> DetailAsync(Guid id, CancellationToken ct)
    {
        SignupApplicationRow row;
        await using (var session = await OpenAsync(ct).ConfigureAwait(false))
            row = await session.Context.Applications.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound);
        var status = SignupStatuses.Parse(row.Status);
        var checks = await ChecksAsync(row, ct).ConfigureAwait(false);
        SignupProvisioning? provisioning = null;
        if (row.ReviewedAt is not null && row.Plan is not null)
        {
            IReadOnlyList<SignupProvisionStep> steps = [];
            if (row.ProvisionedTenantId is { } tenant)
            {
                try
                {
                    steps = [.. (await Provisioning.GetStepsAsync(tenant, ct).ConfigureAwait(false)).Select(step => new SignupProvisionStep(step.Step, step.Status, step.Attempts) { Reason = step.Reason })];
                }
                catch (QianErrorException error) when (error.Error.Code == PlatformErrors.NotFound) { }
            }
            provisioning = new SignupProvisioning(row.Attempts, steps) { TenantId = row.ProvisionedTenantId, MembershipId = row.MembershipId, FailureReason = row.FailureReason };
        }
        return new SignupApplicationDetail(row.Id, row.Number, status, new SignupCompany(row.CompanyName, row.Code, row.Size) { Industry = row.Industry, Description = row.Description },
            new SignupApplicantProfile(row.PersonId, row.ContactName, SignupRules.MaskPhone(row.Phone), row.ExistingAccount) { Email = row.Email },
            checks, new SignupReviewDefaults(_options.TrialDays, _options.DefaultUsers), row.SubmittedAt)
        {
            CompletedAt = row.CompletedAt,
            Review = row.Reviewer is null || row.ReviewedAt is null ? null : new SignupReview(row.Reviewer, row.ReviewedAt.Value)
            {
                Plan = Plan(row.Plan), ValidUntil = row.Plan is null ? null : row.ValidUntil, Users = row.Users, RejectReason = row.RejectReason, Note = row.ReviewNote
            },
            Provisioning = provisioning
        };
    }

    // 自动检查只对进行中的申请有意义（已通过的租户号就是它自己建的租户，已驳回与已撤回的已释放）；终态返回空集合。
    private async Task<IReadOnlyList<SignupCheck>> ChecksAsync(SignupApplicationRow row, CancellationToken ct)
    {
        if (!SignupStatuses.Open.Contains(row.Status, StringComparer.Ordinal)) return [];
        var checks = new List<SignupCheck>(3);
        // 租户号：开通中 / 开通失败的申请自己可能已经建了这个租户，那不算占用。
        var tenant = await Directory.GetAsync(row.Code, ct).ConfigureAwait(false);
        bool taken;
        if (tenant is not null) taken = tenant.Id != row.ProvisionedTenantId;
        else
        {
            await using var session = await OpenAsync(ct).ConfigureAwait(false);
            taken = await session.Context.Applications.AnyAsync(a => a.Code == row.Code && SignupStatuses.Open.Contains(a.Status) && a.Id != row.Id, ct).ConfigureAwait(false);
        }
        var problem = taken ? SignupCodeReasons.Taken : SignupRules.CodeProblem(row.Code, _options);
        checks.Add(problem is null ? new SignupCheck(SignupCheckKinds.Code, SignupCheckLevels.Pass) : new SignupCheck(SignupCheckKinds.Code, SignupCheckLevels.Error) { Reason = problem });
        // 同名企业：已有租户（按名称精确比较，忽略大小写与首尾空白）与其它进行中的申请。
        var name = row.CompanyName.Trim();
        var (items, _) = await _context.Inject<ITenantOperations>(TenantOperations.Key).QueryAsync(0, 100, null, name, ct).ConfigureAwait(false);
        var sameTenants = items.Count(item => string.Equals(item.Name.Trim(), name, StringComparison.OrdinalIgnoreCase) && item.Id != row.ProvisionedTenantId);
        int sameApplications;
        await using (var session = await OpenAsync(ct).ConfigureAwait(false))
        {
            // 申请里的企业名称提交时已去掉首尾空白；ILIKE 不带通配符即忽略大小写的相等比较。
            var exact = name.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
            sameApplications = await session.Context.Applications.CountAsync(a => a.Id != row.Id && SignupStatuses.Open.Contains(a.Status) && EF.Functions.ILike(a.CompanyName, exact, "\\"), ct).ConfigureAwait(false);
        }
        var duplicates = sameTenants + sameApplications;
        checks.Add(duplicates == 0 ? new SignupCheck(SignupCheckKinds.DuplicateName, SignupCheckLevels.Pass)
            : new SignupCheck(SignupCheckKinds.DuplicateName, SignupCheckLevels.Warning) { Count = duplicates });
        // 申请人账号已加入的企业：身份组件不提供这项查询时记为提示（无法判定），不带数量。
        var memberships = await Accounts.ListPersonTenantsAsync(row.PersonId, ct).ConfigureAwait(false);
        var others = memberships?.Count(item => item != row.ProvisionedTenantId);
        checks.Add(others switch
        {
            null => new SignupCheck(SignupCheckKinds.ExistingMemberships, SignupCheckLevels.Info),
            0 => new SignupCheck(SignupCheckKinds.ExistingMemberships, SignupCheckLevels.Pass) { Count = 0 },
            var count => new SignupCheck(SignupCheckKinds.ExistingMemberships, SignupCheckLevels.Info) { Count = count }
        });
        return checks.AsReadOnly();
    }

    private static readonly string[] Severity = [SignupCheckLevels.Pass, SignupCheckLevels.Info, SignupCheckLevels.Warning, SignupCheckLevels.Error];

    // 开通编排的必要步骤没有成功（跳过也算）：Reason 是步骤表里的稳定原因码。
    private sealed class StepIncompleteException(string reason) : Exception(reason)
    {
        internal string Reason { get; } = reason;
    }

    // 列表每行只带最需要关注的一条：级别最重的；同级取检查顺序靠前的。终态的申请没有检查。
    private static SignupCheck? MostSevere(IReadOnlyList<SignupCheck> checks) =>
        checks.Count == 0 ? null : checks.Select((check, index) => (check, index)).OrderByDescending(item => Array.IndexOf(Severity, item.check.Level)).ThenBy(item => item.index).First().check;
}
