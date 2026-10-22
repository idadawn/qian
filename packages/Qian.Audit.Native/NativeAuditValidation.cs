using System.Data.Common;
using System.Text.Json;
using Qian.Runtime.Errors;

namespace Qian.Audit.Native;

internal static class NativeAuditValidation
{
    private static readonly string[] RequiredIndexes = ["audit_entries:id", "audit_entries:chain_key,seq", "audit_entries:content_hash", "audit_entries:chain_key,prev_hash:nulls-equal", "audit_chain_state:chain_key"];
    internal static async Task ValidateAsync(DbConnection connection, DbTransaction? transaction, NativeAuditRoles roles, bool full, CancellationToken ct)
    {
        var parameters = new (string, object?)[] { ("tenant", roles.TenantLogin), ("platform", roles.PlatformLogin ?? ""), ("writer", roles.FunctionOwner) };
        await using (var command = NativeAuditStore.Command(connection, transaction, NativeAuditStorage.Resource("validate-roles.sql"), parameters))
            if (await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is not true) throw Invalid();
        await using (var command = NativeAuditStore.Command(connection, transaction, """
            SELECT c.relname,p.polname,p.polpermissive,p.polcmd,p.polroles=ARRAY[0::oid],c.relrowsecurity,c.relforcerowsecurity,
              pg_catalog.pg_get_expr(p.polqual,p.polrelid),pg_catalog.pg_get_expr(p.polwithcheck,p.polrelid),pg_catalog.obj_description(p.oid,'pg_policy')
            FROM pg_catalog.pg_policy p JOIN pg_catalog.pg_class c ON c.oid=p.polrelid JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
            WHERE n.nspname='plugin_qian_audit' AND c.relname IN ('audit_entries','audit_chain_state')
              AND p.polname IN ('qian_tenant_guard','qian_audit_native_guard')
            """))
        {
            var count = 0; await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                count++;
                if (reader.GetBoolean(2) || reader.GetChar(3) != '*' || !reader.GetBoolean(4) || !reader.GetBoolean(5) || !reader.GetBoolean(6)) throw Invalid();
                var usingSql = reader.GetString(7); var check = reader.GetString(8);
                if (reader.GetString(1) == NativeAuditStorage.GuardPolicy)
                {
                    if (usingSql != NativeAuditStorage.ExpectedGuard(roles) || check != usingSql) throw Invalid();
                }
                else
                {
                    var note = reader.IsDBNull(9) ? "" : reader.GetString(9); const string prefix = "qian-audit-native-v1:";
                    if (!note.StartsWith(prefix, StringComparison.Ordinal)) throw Invalid();
                    try
                    {
                        using var marker = JsonDocument.Parse(note[prefix.Length..]); var root = marker.RootElement;
                        if (root.GetProperty("tenant").GetString() != roles.TenantLogin || root.GetProperty("platform").GetString() != (roles.PlatformLogin ?? "") ||
                            root.GetProperty("owner").GetString() != roles.FunctionOwner || root.GetProperty("installedUsing").GetString() != usingSql || root.GetProperty("installedCheck").GetString() != check) throw Invalid();
                    }
                    catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException) { throw Invalid(); }
                }
            }
            if (count != 4) throw Invalid();
        }
        await ValidateFunctions(connection, transaction, roles, ct).ConfigureAwait(false);
        if (full) await ValidateShape(connection, transaction, ct).ConfigureAwait(false);
    }

    private static async Task ValidateFunctions(DbConnection connection, DbTransaction? transaction, NativeAuditRoles roles, CancellationToken ct)
    {
        await using var command = NativeAuditStore.Command(connection, transaction, """
            SELECT p.proname,p.prosrc,r.rolname,p.prosecdef,pg_catalog.array_to_string(p.proconfig,','),pg_catalog.oidvectortypes(p.proargtypes),pg_catalog.format_type(p.prorettype,NULL),
              NOT EXISTS (SELECT 1 FROM pg_catalog.aclexplode(COALESCE(p.proacl,pg_catalog.acldefault('f',p.proowner))) a
                WHERE a.privilege_type<>'EXECUTE' OR (a.grantee<>p.proowner AND (a.is_grantable OR a.grantee NOT IN
                  (SELECT rr.oid FROM pg_catalog.pg_roles rr WHERE rr.rolname::text IN (@tenant,@platform) AND p.proname<>'native_context')))),
              CASE WHEN p.proname='native_context' THEN NOT pg_catalog.has_function_privilege(@tenant,p.oid,'EXECUTE')
                ELSE pg_catalog.has_function_privilege(@tenant,p.oid,'EXECUTE') END,
              CASE WHEN @platform='' THEN true WHEN p.proname='native_context' THEN NOT pg_catalog.has_function_privilege(@platform,p.oid,'EXECUTE')
                ELSE pg_catalog.has_function_privilege(@platform,p.oid,'EXECUTE') END,
              l.lanname='plpgsql' AND p.prokind='f' AND NOT p.proisstrict AND NOT p.proleakproof AND p.provolatile='v' AND p.proparallel='u'
                AND p.prosupport=0 AND p.provariadic=0 AND p.pronargdefaults=0 AND p.proretset=(p.proname='native_tip')
                AND COALESCE(pg_catalog.array_to_string(p.proargnames,','),'')=CASE p.proname WHEN 'native_context' THEN '' WHEN 'native_tip' THEN 'p_id,next_seq,previous_hash,existing_entry,tip_entry' ELSE 'p_body,p_previous,p_hash' END
                AND CASE WHEN p.proname='native_tip' THEN p.proallargtypes=ARRAY['uuid'::regtype,'bigint'::regtype,'text'::regtype,'jsonb'::regtype,'jsonb'::regtype]::oid[]
                  AND p.proargmodes=ARRAY['i','t','t','t','t']::"char"[] ELSE p.proallargtypes IS NULL AND p.proargmodes IS NULL END
            FROM pg_catalog.pg_proc p JOIN pg_catalog.pg_namespace n ON n.oid=p.pronamespace JOIN pg_catalog.pg_roles r ON r.oid=p.proowner
            JOIN pg_catalog.pg_language l ON l.oid=p.prolang
            WHERE n.nspname='plugin_qian_audit' AND p.proname IN ('native_context','native_tip','native_append')
            """, ("tenant", roles.TenantLogin), ("platform", roles.PlatformLogin ?? ""));
        var count = 0; await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            count++; var name = reader.GetString(0);
            var arguments = name switch { "native_context" => "", "native_tip" => "uuid", _ => "text, text, text" };
            var returns = name switch { "native_context" => "text", "native_tip" => "record", _ => "void" };
            if (reader.GetString(1) != FunctionBody(name, roles) || reader.GetString(2) != roles.FunctionOwner ||
                reader.GetBoolean(3) != (name != "native_context") || reader.IsDBNull(4) || reader.GetString(4) != "search_path=pg_catalog" ||
                reader.GetString(5) != arguments || reader.GetString(6) != returns || !reader.GetBoolean(7) || !reader.GetBoolean(8) || !reader.GetBoolean(9) || !reader.GetBoolean(10)) throw Invalid();
        }
        if (count != 3) throw Invalid();
    }
    internal static string FunctionBody(string name, NativeAuditRoles roles)
    {
        var source = NativeAuditStorage.Replace(NativeAuditStorage.Resource("functions.sql"), roles);
        var start = source.IndexOf("CREATE OR REPLACE FUNCTION plugin_qian_audit." + name + "(", StringComparison.Ordinal);
        start = source.IndexOf("AS $audit_native$", start, StringComparison.Ordinal) + "AS $audit_native$".Length;
        var end = source.IndexOf("$audit_native$;", start, StringComparison.Ordinal);
        return source[start..end];
    }
    private static async Task ValidateShape(DbConnection connection, DbTransaction? transaction, CancellationToken ct)
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["audit_entries.id"]="uuid:true", ["audit_entries.tenant_id"]="character varying(36):false", ["audit_entries.chain_key"]="character varying(64):true",
            ["audit_entries.seq"]="bigint:true", ["audit_entries.prev_hash"]="character varying(64):false", ["audit_entries.content_hash"]="character varying(64):true",
            ["audit_entries.actor_type"]="character varying(16):true", ["audit_entries.actor_id"]="character varying(200):true", ["audit_entries.on_behalf_of"]="character varying(200):false",
            ["audit_entries.capability_id"]="character varying(384):true", ["audit_entries.contract_version"]="character varying(100):true", ["audit_entries.input_hash"]="character varying(64):true",
            ["audit_entries.channel"]="character varying(16):true", ["audit_entries.resource"]="character varying(512):false", ["audit_entries.result"]="character varying(64):true",
            ["audit_entries.trace_id"]="character varying(32):true", ["audit_entries.occurred_at"]="timestamp with time zone:true", ["audit_entries.payload"]="jsonb:true",
            ["audit_chain_state.chain_key"]="character varying(64):true", ["audit_chain_state.tenant_id"]="character varying(36):false", ["audit_chain_state.tip_seq"]="bigint:true", ["audit_chain_state.tip_hash"]="character varying(64):false"
        };
        await using (var command = NativeAuditStore.Command(connection, transaction, """
            SELECT c.relname,a.attname,pg_catalog.format_type(a.atttypid,a.atttypmod),a.attnotnull FROM pg_catalog.pg_attribute a
            JOIN pg_catalog.pg_class c ON c.oid=a.attrelid JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
            WHERE n.nspname='plugin_qian_audit' AND c.relname IN ('audit_entries','audit_chain_state') AND a.attnum>0 AND NOT a.attisdropped
            """))
        {
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var key = reader.GetString(0) + "." + reader.GetString(1);
                if (!expected.Remove(key, out var value) || value != reader.GetString(2) + ":" + (reader.GetBoolean(3) ? "true" : "false")) throw Invalid();
            }
            if (expected.Count != 0) throw Invalid();
        }
        var indexes = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = NativeAuditStore.Command(connection, transaction, """
            SELECT c.relname,pg_catalog.array_to_string(ARRAY(SELECT a.attname FROM pg_catalog.unnest(i.indkey) WITH ORDINALITY k(num,pos)
                JOIN pg_catalog.pg_attribute a ON a.attrelid=c.oid AND a.attnum=k.num ORDER BY k.pos),','),i.indnullsnotdistinct
            FROM pg_catalog.pg_index i JOIN pg_catalog.pg_class c ON c.oid=i.indrelid JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
            WHERE n.nspname='plugin_qian_audit' AND c.relname IN ('audit_entries','audit_chain_state') AND i.indisunique AND i.indisvalid AND i.indisready AND i.indpred IS NULL AND i.indexprs IS NULL
            """))
        {
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
                indexes.Add(reader.GetString(0) + ":" + reader.GetString(1) + (reader.GetBoolean(2) ? ":nulls-equal" : ""));
        }
        if (!RequiredIndexes.All(indexes.Contains)) throw Invalid();
    }
    private static QianErrorException Invalid() => new(new QianError(NativeAuditErrors.Misconfigured));
}
