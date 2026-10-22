CREATE SCHEMA IF NOT EXISTS plugin_qian_audit;
CREATE TABLE IF NOT EXISTS plugin_qian_audit.audit_chain_state (
    chain_key character varying(64) NOT NULL PRIMARY KEY,
    tenant_id character varying(36), tip_seq bigint NOT NULL, tip_hash character varying(64));
CREATE TABLE IF NOT EXISTS plugin_qian_audit.audit_entries (
    id uuid NOT NULL PRIMARY KEY, tenant_id character varying(36), chain_key character varying(64) NOT NULL,
    seq bigint NOT NULL, prev_hash character varying(64), content_hash character varying(64) NOT NULL,
    actor_type character varying(16) NOT NULL, actor_id character varying(200) NOT NULL,
    on_behalf_of character varying(200), capability_id character varying(384) NOT NULL,
    contract_version character varying(100) NOT NULL, input_hash character varying(64) NOT NULL,
    channel character varying(16) NOT NULL, resource character varying(512), result character varying(64) NOT NULL,
    trace_id character varying(32) NOT NULL, occurred_at timestamp with time zone NOT NULL, payload jsonb NOT NULL);
CREATE UNIQUE INDEX IF NOT EXISTS ix_audit_entries_chain_key_seq ON plugin_qian_audit.audit_entries(chain_key,seq);
CREATE UNIQUE INDEX IF NOT EXISTS ix_audit_entries_content_hash ON plugin_qian_audit.audit_entries(content_hash);
CREATE UNIQUE INDEX IF NOT EXISTS ix_audit_entries_chain_key_prev_hash ON plugin_qian_audit.audit_entries(chain_key,prev_hash) NULLS NOT DISTINCT;
CREATE INDEX IF NOT EXISTS ix_audit_entries_tenant_id_on_behalf_of_seq ON plugin_qian_audit.audit_entries(tenant_id,on_behalf_of,seq);
ALTER TABLE plugin_qian_audit.audit_entries ENABLE ROW LEVEL SECURITY;
ALTER TABLE plugin_qian_audit.audit_entries FORCE ROW LEVEL SECURITY;
ALTER TABLE plugin_qian_audit.audit_chain_state ENABLE ROW LEVEL SECURITY;
ALTER TABLE plugin_qian_audit.audit_chain_state FORCE ROW LEVEL SECURITY;

DO $audit_native$
DECLARE tab text;
BEGIN
  FOREACH tab IN ARRAY ARRAY['audit_entries','audit_chain_state'] LOOP
    IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_policy p JOIN pg_catalog.pg_class c ON c.oid=p.polrelid
      JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='plugin_qian_audit' AND c.relname=tab AND p.polname='qian_tenant_access') THEN
      EXECUTE format('CREATE POLICY qian_tenant_access ON plugin_qian_audit.%I FOR ALL USING(true) WITH CHECK(true)',tab);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_policy p JOIN pg_catalog.pg_class c ON c.oid=p.polrelid
      JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='plugin_qian_audit' AND c.relname=tab AND p.polname='qian_tenant_guard') THEN
      EXECUTE format('CREATE POLICY qian_tenant_guard ON plugin_qian_audit.%I AS RESTRICTIVE FOR ALL USING(tenant_id = NULLIF(current_setting(''app.current_tenant_id'',true),'''')) WITH CHECK(tenant_id = NULLIF(current_setting(''app.current_tenant_id'',true),''''))',tab);
    END IF;
  END LOOP;
END $audit_native$;
