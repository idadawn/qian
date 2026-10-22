
CASE
    WHEN (((CURRENT_USER)::text = ANY (ARRAY['$TENANT$'::text, '$PLATFORM$'::text, '$OWNER$'::text])) OR ((SESSION_USER)::text = ANY (ARRAY['$TENANT$'::text, '$PLATFORM$'::text, '$OWNER$'::text]))) THEN ((NOT (EXISTS ( SELECT 1
       FROM (pg_auth_members m
         JOIN pg_roles r ON (((r.oid = m.roleid) OR (r.oid = m.member))))
      WHERE ((r.rolname)::text = ANY (ARRAY['$TENANT$'::text, '$PLATFORM$'::text, '$OWNER$'::text]))))) AND ((((SESSION_USER)::text = '$TENANT$'::text) AND ((CURRENT_USER)::text = ANY (ARRAY['$TENANT$'::text, '$OWNER$'::text])) AND ((tenant_id)::text = NULLIF(current_setting('app.current_tenant_id'::text, true), ''::text))) OR (('$PLATFORM$'::text <> ''::text) AND ((SESSION_USER)::text = '$PLATFORM$'::text) AND ((CURRENT_USER)::text = ANY (ARRAY['$PLATFORM$'::text, '$OWNER$'::text])) AND (tenant_id IS NULL) AND (NULLIF(current_setting('app.current_tenant_id'::text, true), ''::text) IS NULL))))
    ELSE true
END