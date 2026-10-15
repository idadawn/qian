using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;
using Qian.Events;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Tenancy;

internal sealed class TenantStateMachine(IPluginContext context, PostgresTenantDirectory directory) : ITenantLifecycle
{
    private readonly ITenantContext _tenant = context.Services.GetRequiredService<ITenantContext>();
    private TimeProvider Clock => context.Services.GetService<TimeProvider>() ?? TimeProvider.System;
    public ValueTask<TenantDescriptor> ActivateAsync(Guid id, ActivateTenant request, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ValidUntil <= Clock.GetUtcNow()) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        return Change(id, request.Trial ? "trial" : "activate", actor, request.ValidUntil, cancellationToken);
    }
    public ValueTask<TenantDescriptor> SuspendAsync(Guid id, string actor, CancellationToken cancellationToken = default) => Change(id, "suspend", actor, null, cancellationToken);
    public ValueTask<TenantDescriptor> ResumeAsync(Guid id, string actor, CancellationToken cancellationToken = default) => Change(id, "resume", actor, null, cancellationToken);
    public async ValueTask<int> ScanExpiredAsync(int limit, string actor, CancellationToken cancellationToken = default)
    {
        Check(actor); if (limit is < 1 or > 1000) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        Guid[] ids; var now = Clock.GetUtcNow();
        await using (var session = await context.Data().OpenAsync<TenancyDbContext>(cancellationToken).ConfigureAwait(false))
            ids = await session.Context.Tenants.Where(row => (row.Status == "active" || row.Status == "trial") && row.ValidUntil <= now)
                .OrderBy(row => row.Id).Select(row => row.Id).Take(limit).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var changed = 0;
        foreach (var id in ids)
        {
            await Change(id, "expire", actor, null, cancellationToken, () => changed++).ConfigureAwait(false);
        }
        return changed;
    }
    private void Check(string actor)
    {
        if (_tenant.TenantId is not null) throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 200 || actor.Any(char.IsControl)) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
    }
    private async ValueTask<TenantDescriptor> Change(Guid id, string action, string actor, DateTimeOffset? until, CancellationToken ct, Action? transitioned = null)
    {
        Check(actor);
        _ = await directory.GetByIdAsync(id, ct).ConfigureAwait(false) ?? throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        using var tenant = _tenant.BeginScope(id.ToString());
        using var origin = EventEnvelopeScope.Begin(new EventOrigin("qian.tenancy", EventEnvelopeScope.Current?.Actor ?? EventActor.Service(actor)));
        await using var session = await context.Data().OpenAsync<TenancyDbContext>(ct).ConfigureAwait(false);
        await using var transaction = await session.Context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var key = "qian.tenancy:state:" + id;
        await session.Context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct).ConfigureAwait(false);
        var row = await session.Context.Tenants.SingleAsync(item => item.Id == id, ct).ConfigureAwait(false);
        var previous = Parse(row.Status);
        if (action == "expire" && (row.Status is not ("active" or "trial") || row.ValidUntil is null || row.ValidUntil > Clock.GetUtcNow())) return PostgresTenantDirectory.Describe(row);
        var next = Next(previous, action, row.SuspendedFrom is null ? null : Parse(row.SuspendedFrom), row.LastTransition);
        if (next == previous) return (await directory.GetByIdAsync(id, ct).ConfigureAwait(false))!;
        if (action != "expire")
        {
            var decisions = await context.Scoped(new ScopeKey("tenant", id.ToString())).DispatchAsync(TenantLifecycle.Approving,
                new TenantTransitionApproval(id, previous, next, action, actor), ct).ConfigureAwait(false);
            if (decisions.Any(allowed => !allowed)) throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
        }
        if (action == "suspend") row.SuspendedFrom = row.Status;
        if (action is "activate" or "trial") row.ValidUntil = until?.ToUniversalTime();
        row.Status = Name(next); row.LastTransition = action; row.UpdatedBy = actor;
        var kind = action switch { "activate" or "trial" => TenancyEvents.Activated, "suspend" => TenancyEvents.Suspended, "resume" => TenancyEvents.Resumed, _ => TenancyEvents.Expired };
        var change = new TenantChange(kind, row, previous); row.Raise(change);
        try { await TenantChange.Save(session, change, ct).ConfigureAwait(false); }
        catch (DbUpdateConcurrencyException) { throw new QianErrorException(new QianError(PlatformErrors.Conflict)); }
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        transitioned?.Invoke();
        return (await directory.GetByIdAsync(id, ct).ConfigureAwait(false))!;
    }
    internal static TenantStatus Next(TenantStatus state, string action, TenantStatus? previous, string? last) => (state, action) switch
    {
        (>= TenantStatus.Pending and <= TenantStatus.Suspended, "suspend") => TenantStatus.Suspended,
        (TenantStatus.Suspended, "resume") when previous is >= TenantStatus.Pending and < TenantStatus.Suspended => previous.Value,
        (_, "resume") when last == "resume" && state is >= TenantStatus.Pending and < TenantStatus.Suspended => state,
        (TenantStatus.Pending or TenantStatus.Active, "activate") => TenantStatus.Active,
        (TenantStatus.Pending or TenantStatus.Trial, "trial") => TenantStatus.Trial,
        (TenantStatus.Active or TenantStatus.Trial or TenantStatus.Grace, "expire") => TenantStatus.Grace,
        _ => throw new QianErrorException(new QianError(TenancyErrors.StateConflict))
    };
    private static TenantStatus Parse(string value) => value switch { "pending" => TenantStatus.Pending, "active" => TenantStatus.Active, "trial" => TenantStatus.Trial,
        "grace" => TenantStatus.Grace, "suspended" => TenantStatus.Suspended, _ => throw new QianErrorException(new QianError(TenancyErrors.StateConflict)) };
    private static string Name(TenantStatus value) => value.ToString().ToLowerInvariant();
}
