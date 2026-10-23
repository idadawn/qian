using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Qian.Data.Native;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Config.Apollo;

internal sealed class FlagDraftReceipts(IPluginContext owner, ApolloFlagManagementBinding binding)
{
    private readonly string _target = Hash(string.Join('\n', binding.Portal.AbsoluteUri, binding.Environment, binding.AppId, binding.Cluster, "qian.common.flags"));
    internal string TargetHash => _target;
    internal async ValueTask<bool> ClaimAsync(FeatureFlagDraft request, string actor, string ruleHash, CancellationToken ct)
    {
        await using var session = await owner.NativeData().OpenPlatformAsync(binding.ReceiptBinding, owner, ct).ConfigureAwait(false);
        await session.BeginTransactionAsync(cancellationToken: ct).ConfigureAwait(false);
        await ValidateSessionAsync(session, ct).ConfigureAwait(false);
        var hash = RequestHash(request.Name, ruleHash);
        await using var insert = Command(session, """
            INSERT INTO plugin_qian_config_flags.draft_receipts(target_hash,idempotency_key,actor,request_hash,flag_name,rule_hash,state)
            VALUES(@target,@key,@actor,@hash,@name,@rule,'started') ON CONFLICT DO NOTHING
            """, ("target", _target), ("key", request.IdempotencyKey), ("actor", actor), ("hash", hash), ("name", request.Name), ("rule", ruleHash));
        var created = await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false) == 1;
        await using (var select = Command(session, "SELECT actor,request_hash,state FROM plugin_qian_config_flags.draft_receipts WHERE target_hash=@target AND idempotency_key=@key FOR UPDATE",
            ("target", _target), ("key", request.IdempotencyKey)))
        await using (var reader = await select.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(ct).ConfigureAwait(false) || reader.GetString(0) != actor || reader.GetString(1) != hash)
                throw Error(PlatformErrors.Conflict);
            if (!created && reader.GetString(2) != "pending-publish") throw Error(PlatformErrors.Conflict);
        }
        // 先持久化 started 再请求 Portal。进程崩溃或响应丢失时，旧 key 只能明确报冲突，不能盲重发。
        await session.CommitAsync(ct).ConfigureAwait(false);
        return created;
    }
    internal async ValueTask CompleteAsync(Guid key, string state, CancellationToken ct)
    {
        await using var session = await owner.NativeData().OpenPlatformAsync(binding.ReceiptBinding, owner, ct).ConfigureAwait(false);
        await session.BeginTransactionAsync(cancellationToken: ct).ConfigureAwait(false);
        await ValidateSessionAsync(session, ct).ConfigureAwait(false);
        await using var command = Command(session, "UPDATE plugin_qian_config_flags.draft_receipts SET state=@state,completed_at=now() WHERE target_hash=@target AND idempotency_key=@key AND state='started'",
            ("state", state), ("target", _target), ("key", key));
        if (await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) != 1) throw Error(PlatformErrors.Conflict);
        await session.CommitAsync(ct).ConfigureAwait(false);
    }
    internal async ValueTask ValidateAsync(CancellationToken ct)
    {
        await using var session = await owner.NativeData().OpenPlatformAsync(binding.ReceiptBinding, owner, ct).ConfigureAwait(false);
        await session.BeginTransactionAsync(cancellationToken: ct).ConfigureAwait(false);
        await ValidateSessionAsync(session, ct).ConfigureAwait(false);
    }
    private static async ValueTask ValidateSessionAsync(INativeDataSession session, CancellationToken ct)
    {
        // 防止校验到执行之间迁移改变唯一键或ACL；两次操作均持有表锁至事务结束。
        await using (var gate = Command(session, "LOCK TABLE plugin_qian_config_flags.draft_receipts IN ROW SHARE MODE"))
            await gate.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        await using var command = Command(session, """
            SELECT current_user::text, c.relrowsecurity AND c.relforcerowsecurity AND c.relkind='r'
              AND NOT p.polpermissive AND p.polcmd='*' AND p.polroles=ARRAY[0::oid]
              AND pg_catalog.has_table_privilege(current_user,c.oid,'SELECT')
              AND pg_catalog.has_table_privilege(current_user,c.oid,'INSERT')
              AND NOT pg_catalog.has_schema_privilege(current_user,n.oid,'CREATE')
              AND NOT pg_catalog.has_table_privilege(current_user,c.oid,'TRUNCATE')
              AND NOT pg_catalog.has_table_privilege(current_user,c.oid,'TRIGGER')
              AND NOT pg_catalog.has_table_privilege(current_user,c.oid,'DELETE')
              AND pg_catalog.has_column_privilege(current_user,c.oid,'state','UPDATE')
              AND pg_catalog.has_column_privilege(current_user,c.oid,'completed_at','UPDATE')
              AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_attribute a WHERE a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped
                AND a.attname NOT IN ('state','completed_at') AND pg_catalog.has_column_privilege(current_user,c.oid,a.attnum,'UPDATE'))
              AND EXISTS (SELECT 1 FROM pg_catalog.pg_index i WHERE i.indrelid=c.oid AND i.indisprimary AND i.indisvalid AND i.indisready
                AND i.indnkeyatts=2 AND i.indnatts=2 AND i.indexprs IS NULL AND i.indpred IS NULL
                AND (SELECT array_agg(a.attname::text ORDER BY k.position) FROM unnest(i.indkey) WITH ORDINALITY k(number,position)
                  JOIN pg_catalog.pg_attribute a ON a.attrelid=c.oid AND a.attnum=k.number)=ARRAY['target_hash','idempotency_key']),
              pg_catalog.pg_get_expr(p.polqual,p.polrelid),pg_catalog.pg_get_expr(p.polwithcheck,p.polrelid)
            FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
            JOIN pg_catalog.pg_policy p ON p.polrelid=c.oid AND p.polname='flags_platform_guard'
            WHERE n.nspname='plugin_qian_config_flags' AND c.relname='draft_receipts'
            """);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false) || !reader.GetBoolean(1) ||
            reader.GetString(2) != FeatureFlagDraftStorage.ExpectedGuard(reader.GetString(0)) ||
            reader.GetString(3) != FeatureFlagDraftStorage.ExpectedGuard(reader.GetString(0)))
            throw QianConfigurationExtensions.Invalid("flags.receipts");
    }
    private static DbCommand Command(INativeDataSession session, string sql, params (string Name, object Value)[] parameters)
    {
        var command = session.Connection.CreateCommand(); command.Transaction = session.Transaction; command.CommandText = sql;
        foreach (var (name, value) in parameters) { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value; command.Parameters.Add(parameter); }
        return command;
    }
    internal static string RequestHash(string name, string ruleHash) => Hash(name + "\n" + ruleHash);
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static QianErrorException Error(ErrorCode code) => new(new QianError(code));
}
