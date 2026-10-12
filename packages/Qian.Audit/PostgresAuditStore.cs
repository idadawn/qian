using System.Data;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Qian.Data;
using Qian.Runtime.Errors;

namespace Qian.Audit;

// 存储基础，不直接映射为公共写入 API；独立追加的原子性不等于 CRM 业务同事务。
internal sealed class PostgresAuditStore(IDataService data, ITenantContext tenant) : PostgresAuditStore<AuditDbContext>(data, tenant);

internal class PostgresAuditStore<TContext>(IDataService data, ITenantContext tenant, string? platformRole = null) where TContext : AuditDbContext
{
    internal async Task ValidateAsync(CancellationToken ct)
    {
        _ = Owner(); await using var session = await data.OpenAsync<TContext>(ct).ConfigureAwait(false);
        await OpenAsync(session.Context, ct).ConfigureAwait(false);
    }
    internal async Task<AuditChainEntry> AppendAsync(AuditRecord record, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record); var owner = Owner();
        if (record.TenantId != owner) throw Error(PlatformErrors.AuthzDenied);
        // 在建连接前拒绝重复键、过深或不可规范化的补充数据。
        _ = AuditCanonicalizer.Canonicalize(record.Payload);
        for (var attempt = 0; ; attempt++)
        {
            try { return await AppendAttempt(record, owner, ct).ConfigureAwait(false); }
            catch (PostgresException error) when (attempt < 5 && Retryable(error))
            { await Task.Delay(TimeSpan.FromMilliseconds(10 * (attempt + 1)), ct).ConfigureAwait(false); }
            catch (DbUpdateException error) when (attempt < 5 && error.InnerException is PostgresException pg && Retryable(pg))
            { await Task.Delay(TimeSpan.FromMilliseconds(10 * (attempt + 1)), ct).ConfigureAwait(false); }
            catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            { throw Error(PlatformErrors.Conflict); }
        }
    }
    private async Task<AuditChainEntry> AppendAttempt(AuditRecord record, string? owner, CancellationToken ct)
    {
        if (Owner() != owner) throw Error(PlatformErrors.AuthzDenied);
        await using var session = await data.OpenAsync<TContext>(ct).ConfigureAwait(false);
        var db = session.Context; var key = ChainKey(owner);
        await OpenAsync(db, ct).ConfigureAwait(false);
        // 先取得 session 锁，再开始 SERIALIZABLE 快照；避免等待行锁的旧快照在持续追加时饥饿。
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_lock(hashtextextended({"qian.audit:" + key}, 0))", ct).ConfigureAwait(false);

            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
            var existing = await db.Entries.AsNoTracking().SingleOrDefaultAsync(row => row.Id == record.Id, ct).ConfigureAwait(false);
            if (existing is not null)
            {
                var persisted = ToEntry(existing);
                if (AuditCanonicalizer.Hash(persisted.Record, persisted.Sequence, persisted.PreviousHash) != persisted.ContentHash ||
                    AuditCanonicalizer.Hash(record, persisted.Sequence, persisted.PreviousHash) != persisted.ContentHash) throw Error(PlatformErrors.Conflict);
                return persisted;
            }
            var chain = await db.Chains.SingleOrDefaultAsync(row => row.ChainKey == key, ct).ConfigureAwait(false);
            if (chain is null) { chain = new AuditChainRow { TenantId = owner, ChainKey = key }; db.Chains.Add(chain); }
            if (chain.TipSequence < 0 || chain.TipSequence == long.MaxValue || (chain.TipSequence == 0 && chain.TipHash is not null)) throw Error(PlatformErrors.Conflict);
            if (chain.TipSequence > 0)
            {
                var tip = await db.Entries.AsNoTracking().SingleOrDefaultAsync(entry => entry.Sequence == chain.TipSequence, ct).ConfigureAwait(false);
                if (tip is null || tip.ContentHash != chain.TipHash || AuditCanonicalizer.Hash(ToEntry(tip).Record, tip.Sequence, tip.PreviousHash) != tip.ContentHash)
                    throw Error(PlatformErrors.Conflict);
            }
            var sequence = checked(chain.TipSequence + 1);
            var hash = AuditCanonicalizer.Hash(record, sequence, chain.TipHash);
            var row = ToRow(record, sequence, chain.TipHash, hash, key); db.Entries.Add(row);
            chain.TipSequence = sequence; chain.TipHash = hash;
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return ToEntry(row);
        }
        finally { await ReleaseLockAsync(db, key).ConfigureAwait(false); }
    }
    private static async Task ReleaseLockAsync(AuditDbContext db, string key)
    {
        // 解锁不沿用已取消的业务 token；清理失败时清池，避免把可能仍持锁的 session 复用给下一请求。
        try
        {
            if (db.Database.GetDbConnection().State == ConnectionState.Open)
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock(hashtextextended({"qian.audit:" + key}, 0))", CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            if (db.Database.GetDbConnection() is NpgsqlConnection connection) NpgsqlConnection.ClearPool(connection);
            throw;
        }
        finally { await db.Database.CloseConnectionAsync().ConfigureAwait(false); }
    }
    internal async Task<IReadOnlyList<AuditChainEntry>> ReadAsync(long afterSequence, int take, string? onBehalfOf, CancellationToken ct)
    {
        var owner = Owner(); if (afterSequence < 0 || take is < 1 or > 1000) throw Error(PlatformErrors.InputInvalid);
        await using var session = await data.OpenAsync<TContext>(ct).ConfigureAwait(false);
        await OpenAsync(session.Context, ct).ConfigureAwait(false);
        var query = session.Context.Entries.AsNoTracking().Where(row => row.ChainKey == ChainKey(owner) && row.Sequence > afterSequence);
        if (onBehalfOf is not null) query = query.Where(row => row.OnBehalfOf == onBehalfOf);
        return Array.AsReadOnly((await query.OrderBy(row => row.Sequence).Take(take).ToArrayAsync(ct).ConfigureAwait(false)).Select(ToEntry).ToArray());
    }
    internal async Task<AuditChainVerification> VerifyAsync(CancellationToken ct)
    {
        var owner = Owner(); await using var session = await data.OpenAsync<TContext>(ct).ConfigureAwait(false); var db = session.Context;
        await OpenAsync(db, ct).ConfigureAwait(false);
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct).ConfigureAwait(false);
        var tip = await db.Chains.AsNoTracking().SingleOrDefaultAsync(row => row.ChainKey == ChainKey(owner), ct).ConfigureAwait(false);
        long sequence = 0; string? hash = null;
        while (true)
        {
            var rows = await db.Entries.AsNoTracking().Where(row => row.Sequence > sequence).OrderBy(row => row.Sequence).Take(128).ToArrayAsync(ct).ConfigureAwait(false);
            if (rows.Length == 0) break;
            foreach (var row in rows)
            {
                if (row.Sequence != sequence + 1) return new(false, sequence, sequence + 1, "sequence_gap");
                if (row.PreviousHash != hash || row.ChainKey != ChainKey(owner)) return new(false, sequence, row.Sequence, "previous_mismatch");
                try
                {
                    if (AuditCanonicalizer.Hash(ToEntry(row).Record, row.Sequence, row.PreviousHash) != row.ContentHash)
                        return new(false, sequence, row.Sequence, "content_mismatch");
                }
                catch (Exception error) when (error is QianErrorException or JsonException)
                { return new(false, sequence, row.Sequence, "invalid_record"); }
                sequence = row.Sequence; hash = row.ContentHash;
            }
        }
        return (tip?.TipSequence ?? 0) == sequence && tip?.TipHash == hash
            ? new(true, sequence, null, null) : new(false, sequence, sequence + 1, "tip_mismatch");
    }
    private async Task OpenAsync(AuditDbContext db, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        if (platformRole is not null) await AuditPlatformAccess.ValidateConnectionAsync(db.Database.GetDbConnection(), platformRole, ct).ConfigureAwait(false);
    }
    private static string ChainKey(string? owner) => owner is null ? "platform" : "tenant:" + owner;
    private string? Owner()
    {
        var owner = tenant.TenantId;
        if (platformRole is not null)
        {
            if (typeof(TContext) != typeof(PlatformAuditDbContext) || owner is not null) throw Error(PlatformErrors.AuthzDenied);
            return null;
        }
        if (typeof(TContext) == typeof(PlatformAuditDbContext)) throw Error(PlatformErrors.AuthzDenied);
        return Guid.TryParseExact(owner, "D", out var id) && id != Guid.Empty && owner == id.ToString("D") ? owner! : throw Error(PlatformErrors.Unauthenticated);
    }
    private static bool Retryable(PostgresException error) => error.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected;
    private static QianErrorException Error(ErrorCode code) => new(new QianError(code));
    private static AuditEntryRow ToRow(AuditRecord record, long sequence, string? previous, string hash, string key) => new()
    {
        Id = record.Id, TenantId = record.TenantId, ChainKey = key, Sequence = sequence, PreviousHash = previous, ContentHash = hash,
        ActorType = record.ActorType, ActorId = record.ActorId, OnBehalfOf = record.OnBehalfOf, CapabilityId = record.CapabilityId,
        ContractVersion = record.ContractVersion, InputHash = record.InputHash, Channel = record.Channel, Resource = record.Resource,
        Result = record.Result, TraceId = record.TraceId, OccurredAt = record.OccurredAt, Payload = AuditCanonicalizer.Canonicalize(record.Payload)
    };
    private static AuditChainEntry ToEntry(AuditEntryRow row) => new(row.Sequence, row.PreviousHash, row.ContentHash,
        new AuditRecord(row.Id, row.TenantId, row.ActorType, row.ActorId, row.OnBehalfOf, row.CapabilityId, row.ContractVersion,
            row.InputHash, row.Channel, row.Resource, row.Result, row.TraceId, row.OccurredAt, JsonSerializer.Deserialize<JsonElement>(row.Payload)));
}
