SELECT
  current_user=session_user AND current_user::text IN (@tenant,@platform)
  AND (SELECT count(*) FROM pg_catalog.pg_roles r WHERE r.rolname::text IN (@tenant,@platform,@writer)
    AND r.rolcanlogin=(r.rolname::text<>@writer) AND NOT r.rolinherit AND NOT r.rolsuper AND NOT r.rolbypassrls
    AND NOT r.rolcreatedb AND NOT r.rolcreaterole AND NOT r.rolreplication) = (2+CASE WHEN @platform='' THEN 0 ELSE 1 END)
  AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_auth_members m JOIN pg_catalog.pg_roles r ON (r.oid=m.roleid OR r.oid=m.member) WHERE r.rolname::text IN (@tenant,@platform,@writer))
  AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_namespace n JOIN pg_catalog.pg_roles r ON r.oid=n.nspowner WHERE n.nspname='plugin_qian_audit' AND r.rolname::text IN (@tenant,@platform,@writer))
  AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace JOIN pg_catalog.pg_roles r ON r.oid=c.relowner
    WHERE n.nspname='plugin_qian_audit' AND c.relname IN ('audit_entries','audit_chain_state') AND r.rolname::text IN (@tenant,@platform,@writer))
  AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles r WHERE r.rolname::text IN (@tenant,@platform,@writer)
    AND (NOT pg_catalog.has_schema_privilege(r.oid,'plugin_qian_audit','USAGE') OR pg_catalog.has_schema_privilege(r.oid,'plugin_qian_audit','CREATE')))
  AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles r CROSS JOIN pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
    WHERE r.rolname::text IN (@tenant,@platform) AND n.nspname='plugin_qian_audit' AND c.relname IN ('audit_entries','audit_chain_state')
      AND (NOT pg_catalog.has_table_privilege(r.oid,c.oid,'SELECT') OR pg_catalog.has_table_privilege(r.oid,c.oid,'INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')))
  AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles r CROSS JOIN pg_catalog.pg_attribute a JOIN pg_catalog.pg_class c ON c.oid=a.attrelid JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
    WHERE r.rolname::text IN (@tenant,@platform) AND n.nspname='plugin_qian_audit' AND c.relname IN ('audit_entries','audit_chain_state') AND a.attnum>0 AND NOT a.attisdropped
      AND pg_catalog.has_column_privilege(r.oid,c.oid,a.attnum,'INSERT,UPDATE,REFERENCES'))
  AND pg_catalog.has_table_privilege(@writer,'plugin_qian_audit.audit_entries','SELECT') AND pg_catalog.has_table_privilege(@writer,'plugin_qian_audit.audit_entries','INSERT')
  AND pg_catalog.has_table_privilege(@writer,'plugin_qian_audit.audit_chain_state','SELECT') AND pg_catalog.has_table_privilege(@writer,'plugin_qian_audit.audit_chain_state','INSERT')
  AND NOT pg_catalog.has_table_privilege(@writer,'plugin_qian_audit.audit_entries','UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')
  AND NOT pg_catalog.has_table_privilege(@writer,'plugin_qian_audit.audit_chain_state','UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')
  AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_attribute a JOIN pg_catalog.pg_class c ON c.oid=a.attrelid JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace
    WHERE n.nspname='plugin_qian_audit' AND c.relname IN ('audit_entries','audit_chain_state') AND a.attnum>0 AND NOT a.attisdropped
      AND (pg_catalog.has_column_privilege(@writer,c.oid,a.attnum,'REFERENCES')
        OR pg_catalog.has_column_privilege(@writer,c.oid,a.attnum,'UPDATE') <> (c.relname='audit_chain_state' AND a.attname IN ('tip_seq','tip_hash'))))
