CREATE OR REPLACE FUNCTION plugin_qian_audit.native_context()
RETURNS text LANGUAGE plpgsql SECURITY INVOKER SET search_path=pg_catalog AS $audit_native$
DECLARE t text;
BEGIN
  IF current_user::text <> '$OWNER$' OR current_setting('transaction_isolation') <> 'serializable'
    OR NOT EXISTS (SELECT 1 FROM pg_roles r WHERE r.rolname::text='$OWNER$' AND NOT r.rolcanlogin AND NOT r.rolinherit
      AND NOT r.rolsuper AND NOT r.rolbypassrls AND NOT r.rolcreatedb AND NOT r.rolcreaterole AND NOT r.rolreplication)
    OR NOT EXISTS (SELECT 1 FROM pg_roles r WHERE r.rolname=session_user AND r.rolcanlogin AND NOT r.rolinherit
      AND NOT r.rolsuper AND NOT r.rolbypassrls AND NOT r.rolcreatedb AND NOT r.rolcreaterole AND NOT r.rolreplication)
    OR EXISTS (SELECT 1 FROM pg_auth_members m JOIN pg_roles r ON (r.oid=m.roleid OR r.oid=m.member)
      WHERE r.rolname::text IN ('$TENANT$','$PLATFORM$','$OWNER$'))
  THEN RAISE EXCEPTION 'native audit dedicated roles and serializable transaction required' USING ERRCODE='42501'; END IF;
  t:=NULLIF(current_setting('app.current_tenant_id',true),'');
  IF session_user::text='$TENANT$' THEN
    IF t IS NULL OR t !~ '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' OR t='00000000-0000-0000-0000-000000000000'
    THEN RAISE EXCEPTION 'native audit tenant required' USING ERRCODE='42501'; END IF;
  ELSIF '$PLATFORM$'<>'' AND session_user::text='$PLATFORM$' THEN
    IF t IS NOT NULL THEN RAISE EXCEPTION 'native audit platform cannot enter tenant' USING ERRCODE='42501'; END IF;
  ELSE RAISE EXCEPTION 'native audit caller refused' USING ERRCODE='42501'; END IF;
  RETURN t;
END $audit_native$;

CREATE OR REPLACE FUNCTION plugin_qian_audit.native_tip(p_id uuid)
RETURNS TABLE(next_seq bigint,previous_hash text,existing_entry jsonb,tip_entry jsonb)
LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $audit_native$
DECLARE t text; k text; n bigint; h text;
BEGIN
  t:=plugin_qian_audit.native_context(); k:=CASE WHEN t IS NULL THEN 'platform' ELSE 'tenant:'||t END;
  PERFORM pg_advisory_xact_lock(hashtextextended('qian.audit:'||k,0));
  RETURN QUERY SELECT e.seq,e.prev_hash::text,to_jsonb(e),NULL::jsonb
    FROM plugin_qian_audit.audit_entries e WHERE e.id=p_id AND e.tenant_id IS NOT DISTINCT FROM t;
  IF FOUND THEN RETURN; END IF;
  SELECT c.tip_seq,c.tip_hash INTO n,h FROM plugin_qian_audit.audit_chain_state c
    WHERE c.chain_key=k AND c.tenant_id IS NOT DISTINCT FROM t FOR UPDATE;
  IF NOT FOUND THEN n:=0; h:=NULL; END IF;
  IF n<0 OR n=9223372036854775807 OR (n=0 AND h IS NOT NULL)
  THEN RAISE EXCEPTION 'native audit invalid tip' USING ERRCODE='23514'; END IF;
  RETURN QUERY SELECT n+1,h,NULL::jsonb,
    (SELECT to_jsonb(e) FROM plugin_qian_audit.audit_entries e WHERE e.chain_key=k AND e.seq=n AND e.tenant_id IS NOT DISTINCT FROM t);
END $audit_native$;

CREATE OR REPLACE FUNCTION plugin_qian_audit.native_append(p_body text,p_previous text,p_hash text)
RETURNS void LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $audit_native$
DECLARE b jsonb; t text; k text; n bigint; h text; event_id uuid;
BEGIN
  t:=plugin_qian_audit.native_context();
  IF p_body IS NULL OR octet_length(p_body)>1048576 THEN RAISE EXCEPTION 'native audit body limit' USING ERRCODE='22023'; END IF;
  b:=p_body::jsonb;
  IF b->>'tenant_id' IS DISTINCT FROM t THEN RAISE EXCEPTION 'native audit tenant mismatch' USING ERRCODE='42501'; END IF;
  IF (jsonb_typeof(b)='object' AND b->>'format_version'='v1'
    AND b ?& ARRAY['id','tenant_id','actor_type','actor_id','on_behalf_of','capability_id','contract_version','input_hash','channel','resource','result','trace_id','occurred_at','payload','seq','format_version']
    AND (SELECT count(*) FROM jsonb_object_keys(b))=16 AND jsonb_typeof(b->'payload')='object'
    AND b->>'actor_type' IN ('user','service','agent') AND b->>'channel' IN ('ui','api','mcp','a2a','job','event','hook')
    AND b->>'input_hash' ~ '^[a-f0-9]{64}$' AND b->>'trace_id' ~ '^[a-f0-9]{32}$'
    AND b->>'seq' ~ '^[1-9][0-9]*$' AND p_hash ~ '^[a-f0-9]{64}$') IS DISTINCT FROM TRUE
  THEN RAISE EXCEPTION 'native audit invalid record' USING ERRCODE='22023'; END IF;
  event_id:=(b->>'id')::uuid; k:=CASE WHEN t IS NULL THEN 'platform' ELSE 'tenant:'||t END;
  IF event_id='00000000-0000-0000-0000-000000000000'::uuid
    OR p_hash IS DISTINCT FROM encode(sha256(convert_to(COALESCE(p_previous,'')||p_body,'UTF8')),'hex')
  THEN RAISE EXCEPTION 'native audit invalid digest' USING ERRCODE='23514'; END IF;
  PERFORM pg_advisory_xact_lock(hashtextextended('qian.audit:'||k,0));
  SELECT c.tip_seq,c.tip_hash INTO n,h FROM plugin_qian_audit.audit_chain_state c
    WHERE c.chain_key=k AND c.tenant_id IS NOT DISTINCT FROM t FOR UPDATE;
  IF NOT FOUND THEN n:=0; h:=NULL; END IF;
  IF n<0 OR n=9223372036854775807 OR (b->>'seq')::bigint<>n+1 OR p_previous IS DISTINCT FROM h
  THEN RAISE EXCEPTION 'native audit chain changed' USING ERRCODE='40001'; END IF;
  INSERT INTO plugin_qian_audit.audit_chain_state(chain_key,tenant_id,tip_seq,tip_hash)
    VALUES(k,t,0,NULL) ON CONFLICT(chain_key) DO NOTHING;
  INSERT INTO plugin_qian_audit.audit_entries
    (id,tenant_id,chain_key,seq,prev_hash,content_hash,actor_type,actor_id,on_behalf_of,capability_id,contract_version,input_hash,channel,resource,result,trace_id,occurred_at,payload)
  VALUES(event_id,t,k,(b->>'seq')::bigint,p_previous,p_hash,b->>'actor_type',b->>'actor_id',b->>'on_behalf_of',
    b->>'capability_id',b->>'contract_version',b->>'input_hash',b->>'channel',b->>'resource',b->>'result',b->>'trace_id',(b->>'occurred_at')::timestamptz,b->'payload');
  UPDATE plugin_qian_audit.audit_chain_state SET tip_seq=(b->>'seq')::bigint,tip_hash=p_hash
    WHERE chain_key=k AND tenant_id IS NOT DISTINCT FROM t;
END $audit_native$;
