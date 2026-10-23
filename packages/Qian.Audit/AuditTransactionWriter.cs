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
    // 平台链的同事务入口（S1-AUD-009）：调用方须无租户上下文、记录无租户；连接是受限登录（非 superuser / BYPASSRLS、未 SET ROLE、不是平台登录）、
    // 没有租户 GUC，并被直接授予 platform_tip / platform_append 的执行权（与函数自身的调用方检查同一口径；宿主漏了 AuditPlatformAccess 三参迁移时在这里拒绝，而不是在函数调用处报数据库错误）。
    // 追加由函数以属主身份完成；函数只返回序号与哈希，不把平台记录内容交给调用方，所以不重放链头记录的哈希，链头一致性由函数核对、全链由平台登录验链。
    internal static async Task<AuditChainEntry> AppendPlatformAsync(AuditRecord record, DbTransaction transaction, ITenantContext tenant, string platformRole, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record); ArgumentNullException.ThrowIfNull(transaction);
        if (transaction is not NpgsqlTransaction pg || pg.Connection is not { State: ConnectionState.Open } connection || pg.IsolationLevel != IsolationLevel.Serializable)
            throw Error(PlatformErrors.InputInvalid);
        if (tenant.TenantId is not null || record.TenantId is not null) throw Error(PlatformErrors.AuthzDenied);
        _ = AuditCanonicalizer.Canonicalize(record.Payload);
        await using (var guard = new NpgsqlCommand(PlatformGuard, connection, pg))
        {
            guard.Parameters.AddWithValue("platform", platformRole);
            if (await guard.ExecuteScalarAsync(ct).ConfigureAwait(false) is not true) throw Error(PlatformErrors.AuthzDenied);
        }
        long sequence; string? previous;
        try
        {
            await using var tip = new NpgsqlCommand("SELECT * FROM plugin_qian_audit.platform_tip(@id)", connection, pg);
            tip.Parameters.AddWithValue("id", record.Id);
            await using var reader = await tip.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) throw Error(PlatformErrors.Conflict);
            if (!reader.IsDBNull(2))
            {
                // 同一事件 ID 已在平台链上：只有完整内容一致（同序号、同前序下哈希相同）才幂等返回，否则 CONFLICT。
                var (existingSequence, existingPrevious, existingHash) = (reader.GetInt64(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetString(4));
                if (AuditCanonicalizer.Hash(record, existingSequence, existingPrevious) != existingHash) throw Error(PlatformErrors.Conflict);
                return new AuditChainEntry(existingSequence, existingPrevious, existingHash, record);
            }
            sequence = reader.GetInt64(0); previous = reader.IsDBNull(1) ? null : reader.GetString(1);
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.CheckViolation)
        { throw Error(PlatformErrors.Conflict); }
        var hash = AuditCanonicalizer.Hash(record, sequence, previous);
        await using var append = new NpgsqlCommand("SELECT plugin_qian_audit.platform_append(@body, @previous, @hash)", connection, pg);
        append.Parameters.AddWithValue("body", AuditCanonicalizer.Content(record, sequence));
        append.Parameters.AddWithValue("previous", NpgsqlDbType.Text, (object?)previous ?? DBNull.Value);
        append.Parameters.AddWithValue("hash", hash);
        await append.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        return new AuditChainEntry(sequence, previous, hash, record);
    }
    private const string PlatformGuard = """
        SELECT NOT coalesce(bool_or(rolsuper OR rolbypassrls), true) AND current_user = session_user AND current_user::text <> @platform
          AND NULLIF(current_setting('app.current_tenant_id', true), '') IS NULL
          AND (SELECT count(*) FROM pg_catalog.pg_proc p CROSS JOIN LATERAL pg_catalog.aclexplode(p.proacl) a
            WHERE p.oid IN (pg_catalog.to_regprocedure('plugin_qian_audit.platform_tip(uuid)'), pg_catalog.to_regprocedure('plugin_qian_audit.platform_append(text,text,text)'))
              AND a.privilege_type = 'EXECUTE' AND a.grantee = (SELECT r.oid FROM pg_catalog.pg_roles r WHERE r.rolname = session_user)) = 2
        FROM pg_catalog.pg_roles WHERE rolname IN (current_user, session_user)
        """;
    private static AuditChainEntry ReadEntry(string json)
    {
        using var document = JsonDocument.Parse(json); var row = document.RootElement;
        return AuditProtocol.ReadEntry(row);
    }
    private static QianErrorException Error(ErrorCode code) => new(new QianError(code));
}
