DO $audit_native$
DECLARE tab text; guard_oid oid; original_using text; original_check text; installed_using text; installed_check text;
  note text; marker jsonb; obj record; identity_sql text;
BEGIN
  IF (SELECT count(*) FROM pg_catalog.pg_roles r WHERE r.rolname::text IN ('$TENANT$','$PLATFORM$','$OWNER$')
      AND r.rolcanlogin=(r.rolname::text<>'$OWNER$') AND NOT r.rolinherit AND NOT r.rolsuper AND NOT r.rolbypassrls
      AND NOT r.rolcreatedb AND NOT r.rolcreaterole AND NOT r.rolreplication) <> (2+CASE WHEN '$PLATFORM$'='' THEN 0 ELSE 1 END)
    OR EXISTS (SELECT 1 FROM pg_catalog.pg_auth_members m JOIN pg_catalog.pg_roles r ON (r.oid=m.roleid OR r.oid=m.member)
      WHERE r.rolname::text IN ('$TENANT$','$PLATFORM$','$OWNER$'))
  THEN RAISE EXCEPTION 'native audit requires distinct dedicated restricted roles'; END IF;
  IF EXISTS (SELECT 1 FROM pg_catalog.pg_namespace n JOIN pg_catalog.pg_roles r ON r.oid=n.nspowner
      WHERE n.nspname='plugin_qian_audit' AND r.rolname::text IN ('$TENANT$','$PLATFORM$','$OWNER$'))
    OR EXISTS (SELECT 1 FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
      JOIN pg_catalog.pg_roles r ON r.oid=c.relowner WHERE n.nspname='plugin_qian_audit' AND c.relname IN ('audit_entries','audit_chain_state')
      AND r.rolname::text IN ('$TENANT$','$PLATFORM$','$OWNER$'))
  THEN RAISE EXCEPTION 'native audit roles cannot own audit schema or tables'; END IF;

  identity_sql:=$identity$current_user::text IN ('$TENANT$','$PLATFORM$','$OWNER$') OR session_user::text IN ('$TENANT$','$PLATFORM$','$OWNER$')$identity$;
  FOREACH tab IN ARRAY ARRAY['audit_entries','audit_chain_state'] LOOP
    SELECT p.oid,pg_catalog.pg_get_expr(p.polqual,p.polrelid),COALESCE(pg_catalog.pg_get_expr(p.polwithcheck,p.polrelid),pg_catalog.pg_get_expr(p.polqual,p.polrelid)),pg_catalog.obj_description(p.oid,'pg_policy')
      INTO guard_oid,original_using,original_check,note FROM pg_catalog.pg_policy p JOIN pg_catalog.pg_class c ON c.oid=p.polrelid
      JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='plugin_qian_audit' AND c.relname=tab
      AND p.polname='qian_tenant_guard' AND NOT p.polpermissive AND p.polcmd='*' AND p.polroles=ARRAY[0::oid];
    IF guard_oid IS NULL OR original_using IS NULL THEN RAISE EXCEPTION 'expected legacy restrictive tenant guard missing'; END IF;
    IF note LIKE 'qian-audit-native-v1:%' THEN
      marker:=substr(note,length('qian-audit-native-v1:')+1)::jsonb;
      IF marker->>'tenant' IS DISTINCT FROM '$TENANT$' OR marker->>'platform' IS DISTINCT FROM '$PLATFORM$' OR marker->>'owner' IS DISTINCT FROM '$OWNER$'
        OR marker->>'installedUsing' IS DISTINCT FROM original_using OR marker->>'installedCheck' IS DISTINCT FROM original_check
      THEN RAISE EXCEPTION 'native audit guard changed; explicit migration required'; END IF;
    ELSE
      -- 新路径不得复用已拥有审计权限或已有函数的旧 EF 身份。
      IF EXISTS (SELECT 1 FROM pg_catalog.pg_roles r WHERE r.rolname::text IN ('$TENANT$','$PLATFORM$','$OWNER$')
          AND pg_catalog.has_table_privilege(r.oid,format('plugin_qian_audit.%I',tab),'SELECT,INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER'))
        OR EXISTS (SELECT 1 FROM pg_catalog.pg_proc p JOIN pg_catalog.pg_namespace n ON n.oid=p.pronamespace
          JOIN pg_catalog.pg_roles r ON r.oid=p.proowner WHERE n.nspname='plugin_qian_audit' AND r.rolname::text IN ('$TENANT$','$PLATFORM$','$OWNER$'))
        OR EXISTS (SELECT 1 FROM pg_catalog.pg_proc p JOIN pg_catalog.pg_namespace n ON n.oid=p.pronamespace,
          LATERAL pg_catalog.aclexplode(COALESCE(p.proacl,pg_catalog.acldefault('f',p.proowner))) a
          JOIN pg_catalog.pg_roles r ON r.oid=a.grantee WHERE n.nspname='plugin_qian_audit' AND r.rolname::text IN ('$TENANT$','$PLATFORM$','$OWNER$'))
        OR EXISTS (SELECT 1 FROM pg_catalog.pg_roles r CROSS JOIN pg_catalog.pg_attribute a JOIN pg_catalog.pg_class c ON c.oid=a.attrelid
          JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace WHERE r.rolname::text IN ('$TENANT$','$PLATFORM$','$OWNER$')
          AND n.nspname='plugin_qian_audit' AND c.relname=tab AND a.attnum>0 AND NOT a.attisdropped
          AND pg_catalog.has_column_privilege(r.oid,c.oid,a.attnum,'SELECT,INSERT,UPDATE,REFERENCES'))
      THEN RAISE EXCEPTION 'native audit roles must not reuse existing EF audit grants'; END IF;
      EXECUTE format('ALTER POLICY qian_tenant_guard ON plugin_qian_audit.%I USING (((%s) AND NOT (%s)) OR ((%s) AND (%s))) WITH CHECK (((%s) AND NOT (%s)) OR ((%s) AND (%s)))',
        tab,original_using,identity_sql,identity_sql,$guard$$GUARD$$guard$,original_check,identity_sql,identity_sql,$guard$$GUARD$$guard$);
      SELECT pg_catalog.pg_get_expr(p.polqual,p.polrelid),pg_catalog.pg_get_expr(p.polwithcheck,p.polrelid) INTO installed_using,installed_check FROM pg_catalog.pg_policy p WHERE p.oid=guard_oid;
      marker:=jsonb_build_object('tenant','$TENANT$','platform','$PLATFORM$','owner','$OWNER$','originalUsing',original_using,'originalCheck',original_check,'previousComment',note,'installedUsing',installed_using,'installedCheck',installed_check);
      EXECUTE format('COMMENT ON POLICY qian_tenant_guard ON plugin_qian_audit.%I IS %L',tab,'qian-audit-native-v1:'||marker::text);
    END IF;
    EXECUTE format('DROP POLICY IF EXISTS qian_audit_native_guard ON plugin_qian_audit.%I',tab);
    EXECUTE format('CREATE POLICY qian_audit_native_guard ON plugin_qian_audit.%I AS RESTRICTIVE FOR ALL TO PUBLIC USING (%s) WITH CHECK (%s)',tab,$guard$$GUARD$$guard$,$guard$$GUARD$$guard$);
  END LOOP;
END $audit_native$;

GRANT USAGE ON SCHEMA plugin_qian_audit TO "$TENANT$","$OWNER$";
REVOKE ALL ON plugin_qian_audit.audit_entries,plugin_qian_audit.audit_chain_state FROM "$TENANT$","$OWNER$";
GRANT SELECT ON plugin_qian_audit.audit_entries,plugin_qian_audit.audit_chain_state TO "$TENANT$";
GRANT SELECT,INSERT ON plugin_qian_audit.audit_entries,plugin_qian_audit.audit_chain_state TO "$OWNER$";
GRANT UPDATE(tip_seq,tip_hash) ON plugin_qian_audit.audit_chain_state TO "$OWNER$";
GRANT CREATE ON SCHEMA plugin_qian_audit TO "$OWNER$";
DO $audit_native$
BEGIN
  IF '$PLATFORM$'<>'' THEN
    EXECUTE format('GRANT USAGE ON SCHEMA plugin_qian_audit TO %I','$PLATFORM$');
    EXECUTE format('REVOKE ALL ON plugin_qian_audit.audit_entries,plugin_qian_audit.audit_chain_state FROM %I','$PLATFORM$');
    EXECUTE format('GRANT SELECT ON plugin_qian_audit.audit_entries,plugin_qian_audit.audit_chain_state TO %I','$PLATFORM$');
  END IF;
END $audit_native$;
