ALTER FUNCTION plugin_qian_audit.native_context() OWNER TO "$OWNER$";
ALTER FUNCTION plugin_qian_audit.native_tip(uuid) OWNER TO "$OWNER$";
ALTER FUNCTION plugin_qian_audit.native_append(text,text,text) OWNER TO "$OWNER$";
REVOKE CREATE ON SCHEMA plugin_qian_audit FROM "$OWNER$";
REVOKE ALL ON FUNCTION plugin_qian_audit.native_context(),plugin_qian_audit.native_tip(uuid),plugin_qian_audit.native_append(text,text,text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION plugin_qian_audit.native_tip(uuid),plugin_qian_audit.native_append(text,text,text) TO "$TENANT$";
DO $audit_native$
BEGIN
  IF '$PLATFORM$'<>'' THEN
    EXECUTE format('GRANT EXECUTE ON FUNCTION plugin_qian_audit.native_tip(uuid),plugin_qian_audit.native_append(text,text,text) TO %I','$PLATFORM$');
  END IF;
END $audit_native$;
