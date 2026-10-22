using System.Data;
using System.Data.Common;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Npgsql;
using NpgsqlTypes;
using Qian.Runtime.Errors;

namespace Qian.Audit;

internal static class AuditTransactionWriter
{
    internal static async Task<AuditChainEntry> AppendAsync(AuditRecord record, DbTransaction transaction, ITenantContext tenant, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record); ArgumentNullException.ThrowIfNull(transaction);
        if (transaction is not NpgsqlTransaction pg || pg.Connection is not { State: ConnectionState.Open } connection || pg.IsolationLevel != IsolationLevel.Serializable)
            throw Error(PlatformErrors.InputInvalid);
        var owner = tenant.TenantId;
        if (!Guid.TryParseExact(owner, "D", out var tenantId) || tenantId == Guid.Empty || tenantId.ToString("D") != owner || record.TenantId != owner)
            throw Error(PlatformErrors.AuthzDenied);
        _ = AuditCanonicalizer.Canonicalize(record.Payload);
        await using (var guard = new NpgsqlCommand("SELECT NOT coalesce(bool_or(rolsuper OR rolbypassrls), true) AND current_user = session_user AND current_setting('app.current_tenant_id', true) = @tenant FROM pg_catalog.pg_roles WHERE rolname IN (current_user, session_user)", connection, pg))
        {
            guard.Parameters.AddWithValue("tenant", owner);
            if (await guard.ExecuteScalarAsync(ct).ConfigureAwait(false) is not true) throw Error(PlatformErrors.AuthzDenied);
        }
        long sequence; string? previous;
        await using (var tip = new NpgsqlCommand("SELECT * FROM plugin_qian_audit.transaction_tip(@id)", connection, pg))
        {
            tip.Parameters.AddWithValue("id", record.Id);
            await using var reader = await tip.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) throw Error(PlatformErrors.Conflict);
            sequence = reader.GetInt64(0); previous = reader.IsDBNull(1) ? null : reader.GetString(1);
            if (!reader.IsDBNull(2))
            {
                var existing = ReadEntry(reader.GetString(2));
                if (AuditCanonicalizer.Hash(existing.Record, existing.Sequence, existing.PreviousHash) != existing.ContentHash ||
                    AuditCanonicalizer.Hash(record, existing.Sequence, existing.PreviousHash) != existing.ContentHash) throw Error(PlatformErrors.Conflict);
                return existing;
            }
            if (sequence > 1)
            {
                if (reader.IsDBNull(3)) throw Error(PlatformErrors.Conflict);
                var last = ReadEntry(reader.GetString(3));
                if (last.Sequence != sequence - 1 || last.ContentHash != previous || AuditCanonicalizer.Hash(last.Record, last.Sequence, last.PreviousHash) != previous)
                    throw Error(PlatformErrors.Conflict);
            }
        }
        if (tenant.TenantId != owner) throw Error(PlatformErrors.AuthzDenied);
        var hash = AuditCanonicalizer.Hash(record, sequence, previous);
        await using var append = new NpgsqlCommand("SELECT plugin_qian_audit.transaction_append(@body, @previous, @hash)", connection, pg);
        append.Parameters.AddWithValue("body", AuditCanonicalizer.Content(record, sequence));
        append.Parameters.AddWithValue("previous", NpgsqlDbType.Text, (object?)previous ?? DBNull.Value);
        append.Parameters.AddWithValue("hash", hash);
        await append.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return new AuditChainEntry(sequence, previous, hash, record);
    }
    private static AuditChainEntry ReadEntry(string json)
    {
        using var document = JsonDocument.Parse(json); var row = document.RootElement;
        return AuditProtocol.ReadEntry(row);
    }
    private static QianErrorException Error(ErrorCode code) => new(new QianError(code));
}
