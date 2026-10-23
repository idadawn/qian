using System.Data.Common;
using Qian.Data;
using Qian.Runtime.Errors;

namespace Qian.Audit;

/// <summary>平台审计链的显式存储权限；不授予租户业务操作权限，也不替代 Authz。</summary>
public static class AuditPlatformAccess
{
    private const string TenantGuc = "NULLIF(pg_catalog.current_setting('app.current_tenant_id', true), '')";

    /// <summary>生成迁移阶段的 SQL：角色须已独立创建为受限 NOINHERIT LOGIN，无角色成员关系。只改 Audit 自有表；平台链只开放独立事务入口。</summary>
    public static string CreateMigrationSql(string platformRole)
    {
        ValidateRoleName(platformRole);
        return PolicySql(platformRole, null);
    }

    /// <summary>在上一重载之外再开放平台链的同事务入口（S1-AUD-009）：SECURITY DEFINER 函数 platform_tip / platform_append 以 writerRole（与租户链函数同一个独立受限 NOLOGIN 属主）
    /// 追加平台记录，producerRole 只获执行权；RLS 为该属主增加一条分支，只在无租户 GUC、session_user 不是平台登录且属主无任何成员关系时放行平台行，平台登录自身的分支不变。
    /// 在租户守卫之后、同一迁移事务应用，与 AuditTransactionAccess.CreateMigrationSql 的先后无关；多个生产方各执行一次（执行权逐个直接授予）。</summary>
    public static string CreateMigrationSql(string platformRole, string writerRole, string producerRole)
    {
        ValidateRoleName(platformRole); ValidateRoleName(writerRole); ValidateRoleName(producerRole);
        if (writerRole == producerRole || writerRole == platformRole || producerRole == platformRole)
            throw new ArgumentException("平台审计登录、函数所有者与生产方角色必须两两分离。", nameof(producerRole));
        return $"""
            DO $qian_audit$
            BEGIN
            {AuditTransactionAccess.RoleGuards(writerRole, producerRole)}
            END $qian_audit$;

            """ + PolicySql(platformRole, writerRole) + FunctionSql(platformRole, writerRole, producerRole);
    }

    // 租户守卫的完整谓词：租户分支（任何非平台身份，含租户链函数的属主）、平台登录分支（current_user 与 session_user 都须是平台登录），
    // 以及可选的函数属主分支（S1-AUD-009）：无租户 GUC、属主身份、调用方不是平台登录、属主无任何成员关系——属主是 NOLOGIN 角色，非超管无法 SET ROLE 成它，
    // 成为它的唯一路径是它拥有的 SECURITY DEFINER 函数；即使日后被授予成员资格，这条分支也随之关闭。
    private static string Predicate(string platformRole, string? writerRole)
    {
        var tenantBranch = $"(tenant_id = {TenantGuc}\n  AND current_user::text <> '{platformRole}' AND session_user::text <> '{platformRole}')";
        var platformBranch = $"(tenant_id IS NULL AND {TenantGuc} IS NULL\n  AND current_user::text = '{platformRole}' AND session_user::text = '{platformRole}')";
        var writerBranch = writerRole is null ? "" :
            $"\n OR (tenant_id IS NULL AND {TenantGuc} IS NULL\n  AND current_user::text = '{writerRole}' AND session_user::text <> '{platformRole}'\n  AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles r JOIN pg_catalog.pg_auth_members m ON (m.roleid = r.oid OR m.member = r.oid) WHERE r.rolname = '{writerRole}'))";
        return $"({tenantBranch}\n OR {platformBranch}{writerBranch})";
    }

    private static string PolicySql(string platformRole, string? writerRole)
    {
        var predicate = Predicate(platformRole, writerRole);
        return $"""
            DO $qian_audit$
            BEGIN
              IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles r WHERE r.rolname = '{platformRole}'
                AND r.rolcanlogin AND NOT r.rolsuper AND NOT r.rolbypassrls AND NOT r.rolcreatedb
                AND NOT r.rolcreaterole AND NOT r.rolreplication AND NOT r.rolinherit
                AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_auth_members m WHERE m.roleid = r.oid OR m.member = r.oid))
              THEN RAISE EXCEPTION 'audit platform role must be a dedicated restricted login'; END IF;
              IF (SELECT count(*) FROM pg_catalog.pg_policy p JOIN pg_catalog.pg_class c ON c.oid = p.polrelid
                JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'plugin_qian_audit'
                AND c.relname IN ('audit_entries', 'audit_chain_state') AND p.polname = 'qian_tenant_guard'
                AND NOT p.polpermissive AND c.relrowsecurity AND c.relforcerowsecurity) <> 2
              THEN RAISE EXCEPTION 'audit tenant guards must already exist'; END IF;
            END $qian_audit$;
            ALTER POLICY qian_tenant_guard ON plugin_qian_audit.audit_entries USING ({predicate}) WITH CHECK ({predicate});
            ALTER POLICY qian_tenant_guard ON plugin_qian_audit.audit_chain_state USING ({predicate}) WITH CHECK ({predicate});
            GRANT USAGE ON SCHEMA plugin_qian_audit TO "{platformRole}";
            GRANT SELECT, INSERT ON plugin_qian_audit.audit_entries TO "{platformRole}";
            GRANT SELECT, INSERT, UPDATE ON plugin_qian_audit.audit_chain_state TO "{platformRole}";
            REVOKE UPDATE, DELETE, TRUNCATE ON plugin_qian_audit.audit_entries FROM "{platformRole}";
            REVOKE DELETE, TRUNCATE ON plugin_qian_audit.audit_chain_state FROM "{platformRole}";

            """;
    }

    // 两个平台函数共用的调用方检查（函数内 current_user 固定是属主）。事务：SERIALIZABLE 且无租户 GUC。调用方：session_user 不是平台登录、是受限登录（非 superuser / BYPASSRLS），
    // 且本函数的执行权是直接授予它的——借成员关系继承执行权、或 SET ROLE 成生产方的其它登录都被拒绝；属主无任何成员关系。
    private static string CallerGuard(string platformRole, string writerRole, string function) => $"""
          IF {TenantGuc} IS NOT NULL OR pg_catalog.current_setting('transaction_isolation') <> 'serializable'
          THEN RAISE EXCEPTION 'platform audit requires a serializable transaction without tenant' USING ERRCODE = '42501'; END IF;
          IF session_user::text = '{platformRole}'
            OR NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles r WHERE r.rolname = session_user AND r.rolcanlogin AND NOT r.rolsuper AND NOT r.rolbypassrls)
            OR NOT EXISTS (SELECT 1 FROM pg_catalog.pg_proc p CROSS JOIN LATERAL pg_catalog.aclexplode(p.proacl) a
              WHERE p.oid = 'plugin_qian_audit.{function}'::pg_catalog.regprocedure AND a.privilege_type = 'EXECUTE'
                AND a.grantee = (SELECT r.oid FROM pg_catalog.pg_roles r WHERE r.rolname = session_user))
            OR EXISTS (SELECT 1 FROM pg_catalog.pg_roles r JOIN pg_catalog.pg_auth_members m ON (m.roleid = r.oid OR m.member = r.oid) WHERE r.rolname = '{writerRole}')
          THEN RAISE EXCEPTION 'platform audit caller refused' USING ERRCODE = '42501'; END IF;
        """;

    // platform_tip 只返回序号与哈希（下一个序号、链头哈希；同一事件 ID 已在平台链上时返回它的序号、前序与内容哈希），不把平台记录内容交给调用方；
    // 链头一致性（链头行存在且内容哈希等于 tip_hash）在这里核对，不一致报 23514，写入器转成 CONFLICT。
    private static string FunctionSql(string platformRole, string writerRole, string producerRole) => $$"""
        {{AuditTransactionAccess.WriterGrants(writerRole)}}

        CREATE OR REPLACE FUNCTION plugin_qian_audit.platform_tip(p_id uuid)
        RETURNS TABLE(next_seq bigint, previous_hash text, existing_seq bigint, existing_previous text, existing_hash text)
        LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $qian_audit$
        DECLARE n bigint; h text;
        BEGIN
        {{CallerGuard(platformRole, writerRole, "platform_tip(uuid)")}}
          PERFORM pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended('qian.audit:platform', 0));
          RETURN QUERY SELECT NULL::bigint, NULL::text, e.seq, e.prev_hash::text, e.content_hash::text
            FROM plugin_qian_audit.audit_entries e WHERE e.id = p_id AND e.tenant_id IS NULL;
          IF FOUND THEN RETURN; END IF;
          SELECT c.tip_seq, c.tip_hash INTO n, h FROM plugin_qian_audit.audit_chain_state c WHERE c.chain_key = 'platform' AND c.tenant_id IS NULL FOR UPDATE;
          IF NOT FOUND THEN n := 0; h := NULL; END IF;
          IF n < 0 OR n = 9223372036854775807 OR (n = 0 AND h IS NOT NULL)
            OR (n > 0 AND NOT EXISTS (SELECT 1 FROM plugin_qian_audit.audit_entries e
              WHERE e.chain_key = 'platform' AND e.seq = n AND e.content_hash = h AND e.tenant_id IS NULL))
          THEN RAISE EXCEPTION 'invalid audit chain tip' USING ERRCODE = '23514'; END IF;
          RETURN QUERY SELECT n + 1, h, NULL::bigint, NULL::text, NULL::text;
        END $qian_audit$;

        CREATE OR REPLACE FUNCTION plugin_qian_audit.platform_append(p_body text, p_previous text, p_hash text)
        RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $qian_audit$
        DECLARE b jsonb; n bigint; h text; event_id uuid;
        BEGIN
        {{CallerGuard(platformRole, writerRole, "platform_append(text,text,text)")}}
          b := p_body::jsonb;
          IF b->>'tenant_id' IS NOT NULL THEN RAISE EXCEPTION 'audit tenant boundary mismatch' USING ERRCODE = '42501'; END IF;
          IF {{AuditTransactionAccess.RecordShape}}
          THEN RAISE EXCEPTION 'invalid audit record' USING ERRCODE = '22023'; END IF;
          event_id := (b->>'id')::uuid;
          IF event_id = '00000000-0000-0000-0000-000000000000'::uuid
            OR p_hash IS DISTINCT FROM pg_catalog.encode(pg_catalog.sha256(pg_catalog.convert_to(COALESCE(p_previous, '') || p_body, 'UTF8')), 'hex')
          THEN RAISE EXCEPTION 'invalid audit digest' USING ERRCODE = '23514'; END IF;
          PERFORM pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended('qian.audit:platform', 0));
          SELECT c.tip_seq, c.tip_hash INTO n, h FROM plugin_qian_audit.audit_chain_state c WHERE c.chain_key = 'platform' AND c.tenant_id IS NULL FOR UPDATE;
          IF NOT FOUND THEN n := 0; h := NULL; END IF;
          IF (b->>'seq')::bigint <> n + 1 OR p_previous IS DISTINCT FROM h
          THEN RAISE EXCEPTION 'audit chain changed' USING ERRCODE = '40001'; END IF;
          INSERT INTO plugin_qian_audit.audit_chain_state(chain_key, tenant_id, tip_seq, tip_hash)
            VALUES ('platform', NULL, 0, NULL) ON CONFLICT (chain_key) DO NOTHING;
          INSERT INTO plugin_qian_audit.audit_entries
            (id,tenant_id,chain_key,seq,prev_hash,content_hash,actor_type,actor_id,on_behalf_of,capability_id,contract_version,input_hash,channel,resource,result,trace_id,occurred_at,payload)
          VALUES (event_id,NULL,'platform',(b->>'seq')::bigint,p_previous,p_hash,b->>'actor_type',b->>'actor_id',b->>'on_behalf_of',
            b->>'capability_id',b->>'contract_version',b->>'input_hash',b->>'channel',b->>'resource',b->>'result',b->>'trace_id',
            (b->>'occurred_at')::timestamptz,b->'payload');
          UPDATE plugin_qian_audit.audit_chain_state SET tip_seq = (b->>'seq')::bigint, tip_hash = p_hash WHERE chain_key = 'platform' AND tenant_id IS NULL;
        END $qian_audit$;

        ALTER FUNCTION plugin_qian_audit.platform_tip(uuid) OWNER TO "{{writerRole}}";
        ALTER FUNCTION plugin_qian_audit.platform_append(text,text,text) OWNER TO "{{writerRole}}";
        REVOKE CREATE ON SCHEMA plugin_qian_audit FROM "{{writerRole}}";
        REVOKE ALL ON FUNCTION plugin_qian_audit.platform_tip(uuid) FROM PUBLIC;
        REVOKE ALL ON FUNCTION plugin_qian_audit.platform_append(text,text,text) FROM PUBLIC;
        GRANT USAGE ON SCHEMA plugin_qian_audit TO "{{producerRole}}";
        GRANT EXECUTE ON FUNCTION plugin_qian_audit.platform_tip(uuid) TO "{{producerRole}}";
        GRANT EXECUTE ON FUNCTION plugin_qian_audit.platform_append(text,text,text) TO "{{producerRole}}";

        """;

    internal static async Task ValidateConnectionAsync(DbConnection connection, string role, CancellationToken ct)
    {
        ValidateRoleName(role); await QianApplicationRoleInterceptor.ValidateAsync(connection, ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (SELECT 1 FROM pg_catalog.pg_roles r WHERE r.rolname = @role
              AND current_user::text = @role AND session_user::text = @role
              AND r.rolcanlogin AND NOT r.rolsuper AND NOT r.rolbypassrls AND NOT r.rolcreatedb
              AND NOT r.rolcreaterole AND NOT r.rolreplication AND NOT r.rolinherit
              AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_auth_members m WHERE m.roleid = r.oid OR m.member = r.oid))
              AND pg_catalog.has_schema_privilege(@role, 'plugin_qian_audit', 'USAGE')
              AND pg_catalog.has_table_privilege(@role, 'plugin_qian_audit.audit_entries', 'SELECT')
              AND pg_catalog.has_table_privilege(@role, 'plugin_qian_audit.audit_entries', 'INSERT')
              AND NOT pg_catalog.has_table_privilege(@role, 'plugin_qian_audit.audit_entries', 'UPDATE')
              AND NOT pg_catalog.has_table_privilege(@role, 'plugin_qian_audit.audit_entries', 'DELETE')
              AND NOT pg_catalog.has_table_privilege(@role, 'plugin_qian_audit.audit_entries', 'TRUNCATE')
              AND pg_catalog.has_table_privilege(@role, 'plugin_qian_audit.audit_chain_state', 'SELECT')
              AND pg_catalog.has_table_privilege(@role, 'plugin_qian_audit.audit_chain_state', 'INSERT')
              AND pg_catalog.has_table_privilege(@role, 'plugin_qian_audit.audit_chain_state', 'UPDATE')
            """;
        var parameter = command.CreateParameter(); parameter.ParameterName = "role"; parameter.Value = role; command.Parameters.Add(parameter);
        if (await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is not true)
            throw new QianDataMisconfiguredException("平台审计必须使用独立的受限登录角色；禁止角色切换、继承或复用普通租户凭据。");
    }
    internal static void ValidateRoleName(string role)
    {
        if (role is not { Length: > 0 and <= 63 } || role[0] is < 'a' or > 'z' || role.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
    }
}
