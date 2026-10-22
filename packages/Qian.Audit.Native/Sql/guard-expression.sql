CASE
  WHEN current_user::text IN ('$TENANT$','$PLATFORM$','$OWNER$') OR session_user::text IN ('$TENANT$','$PLATFORM$','$OWNER$') THEN
    NOT EXISTS (
      SELECT 1 FROM pg_catalog.pg_auth_members m JOIN pg_catalog.pg_roles r ON (r.oid=m.roleid OR r.oid=m.member)
      WHERE r.rolname::text IN ('$TENANT$','$PLATFORM$','$OWNER$'))
    AND (
      (session_user::text='$TENANT$' AND current_user::text IN ('$TENANT$','$OWNER$')
       AND tenant_id::text=NULLIF(pg_catalog.current_setting('app.current_tenant_id',true),''))
      OR ('$PLATFORM$'<>'' AND session_user::text='$PLATFORM$' AND current_user::text IN ('$PLATFORM$','$OWNER$')
       AND tenant_id IS NULL AND NULLIF(pg_catalog.current_setting('app.current_tenant_id',true),'') IS NULL))
  ELSE true
END
