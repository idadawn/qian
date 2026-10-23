namespace Qian.Audit;

/// <summary>通过 Audit 自有函数接入业务事务，不向生产方授予审计表直接访问权。</summary>
public static class AuditTransactionAccess
{
    /// <summary>在 Audit 建表后以迁移管理员执行；writerRole 是独立受限 NOLOGIN 角色，producerRole 仅获函数调用权限。</summary>
    public static string CreateMigrationSql(string writerRole, string producerRole)
    {
        AuditPlatformAccess.ValidateRoleName(writerRole); AuditPlatformAccess.ValidateRoleName(producerRole);
        if (writerRole == producerRole) throw new ArgumentException("审计函数所有者与生产方角色必须分离。", nameof(producerRole));
        return $$"""
            DO $qian_audit$
            BEGIN
            {{RoleGuards(writerRole, producerRole)}}
            END $qian_audit$;
            {{WriterGrants(writerRole)}}

            CREATE OR REPLACE FUNCTION plugin_qian_audit.transaction_tip(p_id uuid)
            RETURNS TABLE(next_seq bigint, previous_hash text, existing_entry jsonb, tip_entry jsonb)
            LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $qian_audit$
            DECLARE t text; k text; n bigint; h text;
            BEGIN
              t := NULLIF(pg_catalog.current_setting('app.current_tenant_id', true), '');
              IF t IS NULL OR pg_catalog.current_setting('transaction_isolation') <> 'serializable'
              THEN RAISE EXCEPTION 'tenant and serializable transaction required' USING ERRCODE = '42501'; END IF;
              k := 'tenant:' || t;
              PERFORM pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended('qian.audit:' || k, 0));
              RETURN QUERY SELECT e.seq, e.prev_hash::text, pg_catalog.to_jsonb(e), NULL::jsonb
                FROM plugin_qian_audit.audit_entries e WHERE e.id = p_id AND e.tenant_id = t;
              IF FOUND THEN RETURN; END IF;
              SELECT c.tip_seq, c.tip_hash INTO n, h FROM plugin_qian_audit.audit_chain_state c WHERE c.chain_key = k AND c.tenant_id = t FOR UPDATE;
              IF NOT FOUND THEN n := 0; h := NULL; END IF;
              IF n < 0 OR n = 9223372036854775807 OR (n = 0 AND h IS NOT NULL)
              THEN RAISE EXCEPTION 'invalid audit chain tip' USING ERRCODE = '23514'; END IF;
              RETURN QUERY SELECT n + 1, h, NULL::jsonb,
                (SELECT pg_catalog.to_jsonb(e) FROM plugin_qian_audit.audit_entries e WHERE e.chain_key = k AND e.seq = n AND e.tenant_id = t);
            END $qian_audit$;

            CREATE OR REPLACE FUNCTION plugin_qian_audit.transaction_append(p_body text, p_previous text, p_hash text)
            RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $qian_audit$
            DECLARE b jsonb; t text; k text; n bigint; h text; event_id uuid;
            BEGIN
              b := p_body::jsonb; t := NULLIF(pg_catalog.current_setting('app.current_tenant_id', true), '');
              IF t IS NULL OR pg_catalog.current_setting('transaction_isolation') <> 'serializable' OR b->>'tenant_id' IS DISTINCT FROM t
              THEN RAISE EXCEPTION 'audit tenant boundary mismatch' USING ERRCODE = '42501'; END IF;
              IF {{RecordShape}}
              THEN RAISE EXCEPTION 'invalid audit record' USING ERRCODE = '22023'; END IF;
              event_id := (b->>'id')::uuid; k := 'tenant:' || t;
              IF event_id = '00000000-0000-0000-0000-000000000000'::uuid
                OR p_hash IS DISTINCT FROM pg_catalog.encode(pg_catalog.sha256(pg_catalog.convert_to(COALESCE(p_previous, '') || p_body, 'UTF8')), 'hex')
              THEN RAISE EXCEPTION 'invalid audit digest' USING ERRCODE = '23514'; END IF;
              PERFORM pg_catalog.pg_advisory_xact_lock(pg_catalog.hashtextextended('qian.audit:' || k, 0));
              SELECT c.tip_seq, c.tip_hash INTO n, h FROM plugin_qian_audit.audit_chain_state c WHERE c.chain_key = k AND c.tenant_id = t FOR UPDATE;
              IF NOT FOUND THEN n := 0; h := NULL; END IF;
              IF (b->>'seq')::bigint <> n + 1 OR p_previous IS DISTINCT FROM h
              THEN RAISE EXCEPTION 'audit chain changed' USING ERRCODE = '40001'; END IF;
              INSERT INTO plugin_qian_audit.audit_chain_state(chain_key, tenant_id, tip_seq, tip_hash)
                VALUES (k, t, 0, NULL) ON CONFLICT (chain_key) DO NOTHING;
              INSERT INTO plugin_qian_audit.audit_entries
                (id,tenant_id,chain_key,seq,prev_hash,content_hash,actor_type,actor_id,on_behalf_of,capability_id,contract_version,input_hash,channel,resource,result,trace_id,occurred_at,payload)
              VALUES (event_id,t,k,(b->>'seq')::bigint,p_previous,p_hash,b->>'actor_type',b->>'actor_id',b->>'on_behalf_of',
                b->>'capability_id',b->>'contract_version',b->>'input_hash',b->>'channel',b->>'resource',b->>'result',b->>'trace_id',
                (b->>'occurred_at')::timestamptz,b->'payload');
              UPDATE plugin_qian_audit.audit_chain_state SET tip_seq = (b->>'seq')::bigint, tip_hash = p_hash WHERE chain_key = k AND tenant_id = t;
            END $qian_audit$;

            ALTER FUNCTION plugin_qian_audit.transaction_tip(uuid) OWNER TO "{{writerRole}}";
            ALTER FUNCTION plugin_qian_audit.transaction_append(text,text,text) OWNER TO "{{writerRole}}";
            REVOKE CREATE ON SCHEMA plugin_qian_audit FROM "{{writerRole}}";
            REVOKE ALL ON FUNCTION plugin_qian_audit.transaction_tip(uuid) FROM PUBLIC;
            REVOKE ALL ON FUNCTION plugin_qian_audit.transaction_append(text,text,text) FROM PUBLIC;
            GRANT USAGE ON SCHEMA plugin_qian_audit TO "{{producerRole}}";
            GRANT EXECUTE ON FUNCTION plugin_qian_audit.transaction_tip(uuid) TO "{{producerRole}}";
            GRANT EXECUTE ON FUNCTION plugin_qian_audit.transaction_append(text,text,text) TO "{{producerRole}}";
            """;
    }

    // 函数所有者须是独立受限的 NOLOGIN、NOINHERIT 角色且无任何成员关系（谁都不能 SET ROLE 成它）；生产方须是受限角色（非 superuser / BYPASSRLS）。
    // 租户链与平台链的同事务函数（AuditPlatformAccess）共用这段前置检查。
    internal static string RoleGuards(string writerRole, string producerRole) => $"""
          IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles r WHERE r.rolname = '{writerRole}'
            AND NOT r.rolcanlogin AND NOT r.rolsuper AND NOT r.rolbypassrls AND NOT r.rolcreatedb
            AND NOT r.rolcreaterole AND NOT r.rolreplication AND NOT r.rolinherit
            AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_auth_members m WHERE m.roleid = r.oid OR m.member = r.oid))
          THEN RAISE EXCEPTION 'audit transaction owner must be a dedicated restricted nologin role'; END IF;
          IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles r WHERE r.rolname = '{producerRole}' AND NOT r.rolsuper AND NOT r.rolbypassrls)
          THEN RAISE EXCEPTION 'audit producer must be a restricted role'; END IF;
        """;

    // 函数所有者只能追加记录与推进链头：表级只有 SELECT / INSERT（链头另有 UPDATE），不能更新、删除或清空历史。CREATE 只为改函数属主临时授予，末尾收回。
    internal static string WriterGrants(string writerRole) => $"""
        GRANT USAGE, CREATE ON SCHEMA plugin_qian_audit TO "{writerRole}";
        GRANT SELECT, INSERT ON plugin_qian_audit.audit_entries TO "{writerRole}";
        GRANT SELECT, INSERT, UPDATE ON plugin_qian_audit.audit_chain_state TO "{writerRole}";
        REVOKE UPDATE, DELETE, TRUNCATE ON plugin_qian_audit.audit_entries FROM "{writerRole}";
        REVOKE DELETE, TRUNCATE ON plugin_qian_audit.audit_chain_state FROM "{writerRole}";
        """;

    // v1 记录正文的形状检查（b 为 jsonb 正文，p_hash 为传入摘要），为真表示正文不合法；租户链与平台链的 append 函数共用。
    internal const string RecordShape = """
        (pg_catalog.jsonb_typeof(b) = 'object' AND b->>'format_version' = 'v1'
            AND b ?& ARRAY['id','tenant_id','actor_type','actor_id','on_behalf_of','capability_id','contract_version','input_hash','channel','resource','result','trace_id','occurred_at','payload','seq','format_version']
            AND (SELECT count(*) FROM pg_catalog.jsonb_object_keys(b)) = 16
            AND pg_catalog.jsonb_typeof(b->'payload') = 'object'
            AND b->>'actor_type' IN ('user','service','agent')
            AND b->>'channel' IN ('ui','api','mcp','a2a','job','event','hook')
            AND b->>'input_hash' ~ '^[a-f0-9]{64}$' AND b->>'trace_id' ~ '^[a-f0-9]{32}$'
            AND b->>'seq' ~ '^[1-9][0-9]*$' AND p_hash ~ '^[a-f0-9]{64}$') IS DISTINCT FROM TRUE
        """;
}
