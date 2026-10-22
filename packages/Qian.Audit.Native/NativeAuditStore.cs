using System.Data;
using System.Data.Common;
using System.Text.Json;
using Qian.Data.Native;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Audit.Native;

internal sealed class NativeAuditStore(INativeDataService data, NativeAuditSettings settings, IPluginContext producer)
{
    internal async Task<AuditChainEntry> AppendAsync(AuditRecord record, bool platform, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record); _ = AuditProtocol.Canonicalize(record.Payload);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var session = await Open(platform, ct).ConfigureAwait(false);
                if (session.TenantId != record.TenantId) throw Error(PlatformErrors.AuthzDenied);
                await NativeAuditValidation.ValidateAsync(session.Connection, null, settings.Roles, false, ct).ConfigureAwait(false);
                var key = "qian.audit:" + ChainKey(session.TenantId);
                try
                {
                    await Execute(session.Connection, null, "SELECT pg_catalog.pg_advisory_lock(pg_catalog.hashtextextended(@key,0))", ct, ("key", key)).ConfigureAwait(false);
                    var transaction = await session.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
                    var result = await AppendInTransaction(record, session.Connection, transaction, ct).ConfigureAwait(false);
                    await session.CommitAsync(ct).ConfigureAwait(false); return result;
                }
                finally
                {
                    // 失败时先回滚，再解除会话锁；NativeData Dispose 另有原始连接清理/丢弃兜底。
                    try
                    {
                        await session.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                        await Execute(session.Connection, null, "SELECT pg_catalog.pg_advisory_unlock(pg_catalog.hashtextextended(@key,0))", CancellationToken.None, ("key", key)).ConfigureAwait(false);
                    }
                    catch (Exception cleanup) when (cleanup is DbException or InvalidOperationException or OperationCanceledException or QianErrorException) { }
                }
            }
            catch (DbException error) when (attempt < settings.MaxAttempts && error.SqlState is "40001" or "40P01")
            { await Task.Delay(TimeSpan.FromMilliseconds(10 * attempt), ct).ConfigureAwait(false); }
            catch (DbException error) when (error.SqlState is "23505" or "23514") { throw Error(PlatformErrors.Conflict); }
            catch (DbException error) when (error.SqlState == "42501") { throw Error(PlatformErrors.AuthzDenied); }
        }
    }

    internal async Task<AuditChainEntry> AppendEnlistedAsync(AuditRecord record, DbTransaction transaction, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record); ArgumentNullException.ThrowIfNull(transaction);
        if (record.TenantId is null || transaction.Connection is not { } connection) throw Error(PlatformErrors.AuthzDenied);
        _ = AuditProtocol.Canonicalize(record.Payload);
        await data.ValidateTenantTransactionAsync(settings.TenantBinding, transaction, ct).ConfigureAwait(false);
        await NativeAuditValidation.ValidateAsync(connection, transaction, settings.Roles, false, ct).ConfigureAwait(false);
        // 外部真实事务（含 EF）保持完整所有权；不提交、不释放、不局部重试。
        return await AppendInTransaction(record, connection, transaction, ct).ConfigureAwait(false);
    }

    private static async Task<AuditChainEntry> AppendInTransaction(AuditRecord record, DbConnection connection, DbTransaction transaction, CancellationToken ct)
    {
        long sequence; string? previous;
        await using (var command = Command(connection, transaction,
            "SELECT next_seq,previous_hash,existing_entry::text,tip_entry::text FROM plugin_qian_audit.native_tip(@id)", ("id", record.Id)))
        {
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) throw Error(PlatformErrors.Conflict);
            sequence = reader.GetInt64(0); previous = reader.IsDBNull(1) ? null : reader.GetString(1);
            if (!reader.IsDBNull(2))
            {
                var existing = Parse(reader.GetString(2));
                if (existing.Record.TenantId != record.TenantId || AuditProtocol.Hash(existing.Record, existing.Sequence, existing.PreviousHash) != existing.ContentHash ||
                    AuditProtocol.Hash(record, existing.Sequence, existing.PreviousHash) != existing.ContentHash) throw Error(PlatformErrors.Conflict);
                return existing;
            }
            if (sequence > 1)
            {
                if (reader.IsDBNull(3)) throw Error(PlatformErrors.Conflict);
                var tip = Parse(reader.GetString(3));
                if (tip.Record.TenantId != record.TenantId || tip.Sequence != sequence - 1 || tip.ContentHash != previous || AuditProtocol.Hash(tip.Record, tip.Sequence, tip.PreviousHash) != previous)
                    throw Error(PlatformErrors.Conflict);
            }
        }
        var hash = AuditProtocol.Hash(record, sequence, previous);
        await Execute(connection, transaction, "SELECT plugin_qian_audit.native_append(@body,@previous,@hash)", ct,
            ("body", AuditProtocol.Content(record, sequence)), ("previous", previous), ("hash", hash)).ConfigureAwait(false);
        return new AuditChainEntry(sequence, previous, hash, record);
    }

    internal async Task<IReadOnlyList<AuditChainEntry>> ReadAsync(bool platform, long afterSequence, int take, string? onBehalfOf, CancellationToken ct)
    {
        if (afterSequence < 0 || take is < 1 or > 1000) throw Error(PlatformErrors.InputInvalid);
        await using var session = await Open(platform, ct).ConfigureAwait(false);
        await NativeAuditValidation.ValidateAsync(session.Connection, null, settings.Roles, false, ct).ConfigureAwait(false);
        await using var command = Command(session.Connection, null,
            "SELECT pg_catalog.to_jsonb(e)::text FROM plugin_qian_audit.audit_entries e WHERE e.chain_key=@chain AND e.seq>@after AND (@actor IS NULL OR e.on_behalf_of=@actor) ORDER BY e.seq LIMIT @take",
            ("chain", ChainKey(session.TenantId)), ("after", afterSequence), ("actor", onBehalfOf), ("take", take));
        var rows = new List<AuditChainEntry>(); await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) rows.Add(Parse(reader.GetString(0)));
        return rows.AsReadOnly();
    }

    internal async Task<AuditChainVerification> VerifyAsync(bool platform, CancellationToken ct)
    {
        await using var session = await Open(platform, ct).ConfigureAwait(false);
        await NativeAuditValidation.ValidateAsync(session.Connection, null, settings.Roles, false, ct).ConfigureAwait(false);
        var transaction = await session.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct).ConfigureAwait(false);
        long tipSequence = 0; string? tipHash = null; var key = ChainKey(session.TenantId);
        await using (var command = Command(session.Connection, transaction, "SELECT tip_seq,tip_hash FROM plugin_qian_audit.audit_chain_state WHERE chain_key=@chain", ("chain", key)))
        {
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false)) { tipSequence = reader.GetInt64(0); tipHash = reader.IsDBNull(1) ? null : reader.GetString(1); }
        }
        long sequence = 0; string? hash = null;
        while (true)
        {
            await using var command = Command(session.Connection, transaction,
                "SELECT pg_catalog.to_jsonb(e)::text FROM plugin_qian_audit.audit_entries e WHERE e.seq>@after AND e.tenant_id IS NOT DISTINCT FROM @tenant ORDER BY e.seq LIMIT 128",
                ("after", sequence), ("tenant", session.TenantId));
            var rows = new List<string>(); await using (var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
                while (await reader.ReadAsync(ct).ConfigureAwait(false)) rows.Add(reader.GetString(0));
            if (rows.Count == 0) break;
            foreach (var text in rows)
            {
                using var document = JsonDocument.Parse(text); var row = document.RootElement; var current = row.GetProperty("seq").GetInt64();
                if (current != sequence + 1) return new(false, sequence, sequence + 1, "sequence_gap");
                try
                {
                    var entry = AuditProtocol.ReadEntry(row);
                    if (entry.PreviousHash != hash || row.GetProperty("chain_key").GetString() != key) return new(false, sequence, current, "previous_mismatch");
                    if (AuditProtocol.Hash(entry.Record, current, hash) != entry.ContentHash) return new(false, sequence, current, "content_mismatch");
                    sequence = current; hash = entry.ContentHash;
                }
                catch (Exception error) when (error is JsonException or QianErrorException) { return new(false, sequence, current, "invalid_record"); }
            }
        }
        return tipSequence == sequence && tipHash == hash ? new(true, sequence, null, null) : new(false, sequence, sequence + 1, "tip_mismatch");
    }

    internal async Task ValidateAsync(bool platform, bool full, CancellationToken ct)
    {
        await using var session = await Open(platform, ct).ConfigureAwait(false);
        await NativeAuditValidation.ValidateAsync(session.Connection, null, settings.Roles, full, ct).ConfigureAwait(false);
    }
    private ValueTask<INativeDataSession> Open(bool platform, CancellationToken ct) => platform
        ? data.OpenPlatformAsync(settings.PlatformBinding ?? throw Error(PlatformErrors.AuthzDenied), producer, ct)
        : data.OpenTenantAsync(settings.TenantBinding, ct);
    internal static DbCommand Command(DbConnection connection, DbTransaction? transaction, string sql, params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value ?? DBNull.Value;
            parameter.DbType = value switch { Guid => DbType.Guid, long => DbType.Int64, int => DbType.Int32, bool => DbType.Boolean, _ => DbType.String };
            command.Parameters.Add(parameter);
        }
        return command;
    }
    internal static async Task Execute(DbConnection connection, DbTransaction? transaction, string sql, CancellationToken ct, params (string Name, object? Value)[] parameters)
    { await using var command = Command(connection, transaction, sql, parameters); await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false); }
    internal static AuditChainEntry Parse(string json) { using var document = JsonDocument.Parse(json); return AuditProtocol.ReadEntry(document.RootElement); }
    private static string ChainKey(string? tenant) => tenant is null ? "platform" : "tenant:" + tenant;
    internal static QianErrorException Error(ErrorCode code) => new(new QianError(code));
}
