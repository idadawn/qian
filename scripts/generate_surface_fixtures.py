#!/usr/bin/env python3
"""生成双侧共用样例；固定哈希由独立 Python 的整数 JCS 子集生成，浮点向量另有文字规范值。"""
import copy
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DESC = '当用户需要只读检查当前组件配置并获得核对说明时使用，不用于任何数据写入操作。'
MANIFEST = {'id':'qian.fixture','version':'1.0.0','description':'声明式表面校验与作用域共享验收组件。','author':{'name':'Qian'},'license':'Apache-2.0','interface':{'displayName':'校验样例','category':'testing'},'inject':[],'provides':[],'policy':{'tier':'official','sandbox':'none','authentication':'ON_INSTALL','egress':['api.example.test']},'scopes':['global','tenant','app','agent-session']}
CASES=[]
def js(value): return json.dumps(value,ensure_ascii=False,indent=2)+'\n'
def md(header,body='这里只描述核对步骤，不执行任何操作。'):
 # JSON 本身是 YAML 1.2 的子集，额外 YAML 风格样例覆盖原生前置区。
 return '---\n'+js(header)+'---\n\n'+body+'\n'
def base(extra=None):
 files={'.qian-plugin/plugin.json':js(MANIFEST)}
 files.update(extra or {});return files
def case(name,files,valid=True,expected=None,options=None,symlinks=None):
 CASES.append({'name':name,'files':files,'options':options or {},'valid':valid,'expected':expected or [],**({'symlinks':symlinks} if symlinks else {})})
def issue(file,pointer,code,line=None): return {'file':file,'pointer':file+'#'+pointer,'code':'SURFACE_'+code,**({'line':line} if line else {})}
def bad(name,files,file,pointer,code,options=None,symlinks=None,line=None):case(name,files,False,[issue(file,pointer,code,line)],options,symlinks)
# 不可见字符（NEL、行分隔符、段分隔符、BOM）在样例文件里写成转义，便于审阅。
INVISIBLE={c:chr(92)+'u%04x'%c for c in (0x85,0x2028,0x2029,0xfeff)}
def skill(**changes): return md({'name':'guide','description':DESC,**changes})
def tool(**changes):
 value={'name':'read','description':DESC,'inputSchema':{'type':'object','additionalProperties':False,'properties':{'message':{'type':'string'}}},'outputSchema':{'type':'object'},'sideEffect':'read','policyHint':'allow','idempotent':True,'async':'none','requiredScopes':[],'requiredSecrets':[],'dataClassification':'internal','contractVersion':'fixture-v1','agent':{'expose':True,'requiresConfirmation':False},'binding':{'kind':'http','url':'https://api.example.test/read','method':'GET'},**changes}
 canonical=json.dumps({k:value[k] for k in ['description','inputSchema','name']},sort_keys=True,ensure_ascii=False,separators=(',',':'))
 value['schemaSha256']=hashlib.sha256(canonical.encode()).hexdigest();return value
def hook(event='files/uploaded', action=None, matcher=None):
 group={'hooks':[action or {'type':'plugin','plugin':'qian.fixture','handler':'observe'}]}
 if matcher is not None:group['matcher']=matcher
 return {'hooks':{event:[group]}}
def manifest(files,**changes):
 value=copy.deepcopy(MANIFEST);value.update(changes);files['.qian-plugin/plugin.json']=js(value);return files
sf='skills/guide/SKILL.md'; af='agents/helper.md'; cf='commands/check.md'; hf='hooks/hooks.json'; mf='.mcp.json';tf='tools/read.json'; pf='config/qian.patch.yml'
case('skill-only-no-src',base({sf:skill()}))
case('secret-permission-objects',manifest(base(),policy={**MANIFEST['policy'],'permissions':{'secrets':[{'name':'oss/*','scope':'platform'},{'name':'db/token','scope':'tenant'}]}}))
bad('secret-permission-string',manifest(base(),policy={**MANIFEST['policy'],'permissions':{'secrets':['oss/*']}}),'.qian-plugin/plugin.json','/policy/permissions/secrets/0','SCHEMA_INVALID')
bad('secret-permission-invalid-scope',manifest(base(),policy={**MANIFEST['policy'],'permissions':{'secrets':[{'name':'oss/*','scope':'k8s'}]}}),'.qian-plugin/plugin.json','/policy/permissions/secrets/0/scope','SCHEMA_INVALID')
case('yaml-front-matter',base({sf:'---\nname: guide\ndescription: '+DESC+'\nmetadata:\n  visibility: internal\n  version: 1.0.0\n---\n\n核对说明。\n'}))
case('overridden-pointer',manifest(base({'knowledge/guide/SKILL.md':skill()}),surfaces={'skills':'./knowledge'}))
case('all-surfaces',base({sf:skill(**{'allowed-tools':['qian.fixture/read']}),af:md({'name':'helper','description':DESC,'tools':['qian.fixture/read'],'skills':['qian.fixture/guide']}),cf:md({'description':'只读核对配置','allowed-tools':['shell(qian dump-config)']},'!`qian dump-config --format json`'),hf:js(hook()),mf:js({'mcpServers':{'sample':{'type':'http','url':'https://api.example.test/mcp/{tenant}','headers':{'Authorization':'Bearer secretRef://tenant/mcp/token'}}}}),tf:js(tool()),pf:'- id: files\n  config:\n    quota: 100\n    apiKey: secretRef://platform/files/key\n'}),options={'capabilities':['qian.fixture/observe']})
case('unknown-agent-event-warning',base({hf:js(hook('FutureHostEvent'))}),options={'capabilities':['qian.fixture/observe']},expected=[issue(hf,'/hooks/FutureHostEvent','REFERENCE_UNKNOWN')])
case('inactive-stdio-on-platform',base({mf:js({'mcpServers':{'local':{'type':'stdio','command':'qian-tool','profiles':['standalone','minimal']}}})}),options={'profile':'platform'})
case('external-reference-warning',base({sf:skill(**{'allowed-tools':['other.plugin/read']})}),expected=[issue(sf,'/allowed-tools/0','REFERENCE_UNKNOWN')])
bad('skill-missing-name',base({sf:md({'description':DESC})}),sf,'/name','SCHEMA_INVALID')
bad('skill-missing-description',base({sf:md({'name':'guide'})}),sf,'/description','SCHEMA_INVALID')
bad('skill-short-description',base({sf:skill(description='短')}),sf,'/description','SCHEMA_INVALID')
bad('skill-name-mismatch',base({sf:skill(name='another')}),sf,'/name','NAME_MISMATCH')
bad('skill-unknown-tool',base({sf:skill(**{'allowed-tools':['qian.fixture/missing']})}),sf,'/allowed-tools/0','REFERENCE_UNKNOWN')
bad('agent-missing-name',base({af:md({'description':DESC})}),af,'/name','SCHEMA_INVALID')
bad('agent-unknown-skill',base({af:md({'name':'helper','description':DESC,'skills':['qian.fixture/missing']})}),af,'/skills/0','REFERENCE_UNKNOWN')
bad('command-missing-description',base({cf:md({})}),cf,'/description','SCHEMA_INVALID')
bad('command-unapproved-shell',base({cf:md({'description':'核对配置'},'!`qian dump-config`')}),cf,'/allowed-tools','POLICY_DENIED')
shell=base({cf:md({'description':'核对配置','allowed-tools':['shell(qian dump-config)']},'!`qian dump-config`')})
bad('command-server-shell',shell,cf,'/allowed-tools','POLICY_DENIED',{'executionPlane':'server'})
bad('community-shell',manifest(copy.deepcopy(shell),id='com.example.fixture',policy={**MANIFEST['policy'],'tier':'community'}),cf,'/allowed-tools','POLICY_DENIED')
bad('hooks-unknown-event',base({hf:js(hook('unknown/event'))}),hf,'/hooks/unknown~1event','REFERENCE_UNKNOWN',{'capabilities':['qian.fixture/observe']})
bad('hooks-unknown-capability',base({hf:js(hook())}),hf,'/hooks/files~1uploaded/0/hooks/0/handler','REFERENCE_UNKNOWN')
bad('hooks-invalid-matcher',base({hf:js(hook(matcher='('))}),hf,'/hooks/files~1uploaded/0/matcher','SYNTAX_INVALID',{'capabilities':['qian.fixture/observe']})
bad('hooks-shell-command',base({hf:js(hook(action={'type':'command','command':['sh','-c','echo unsafe']}))}),hf,'/hooks/files~1uploaded/0/hooks/0/command','POLICY_DENIED',{'allowCommandHooks':True})
bad('mcp-malformed-json',base({mf:'{"mcpServers":'}),mf,'','SYNTAX_INVALID')
bad('mcp-duplicate-server',base({mf:'{"mcpServers":{"same":{"type":"stdio","command":"first"},"same":{"type":"stdio","command":"second"}}}'}),mf,'/mcpServers/same','SYNTAX_INVALID')
bad('mcp-platform-stdio',base({mf:js({'mcpServers':{'local':{'type':'stdio','command':'qian-tool'}}})}),mf,'/mcpServers/local/type','POLICY_DENIED',{'profile':'platform'})
bad('mcp-plaintext-token',base({mf:js({'mcpServers':{'local':{'type':'stdio','command':'qian-tool','env':{'QIAN_TOKEN':'plain-test-value'}}}})}),mf,'/mcpServers/local/env/QIAN_TOKEN','SECRET_LITERAL')
bad('mcp-unapproved-egress',base({mf:js({'mcpServers':{'remote':{'type':'http','url':'https://outside.example.test/mcp'}}})}),mf,'/mcpServers/remote/url','POLICY_DENIED')
bad('tool-nested-tenant',base({tf:js(tool(inputSchema={'type':'object','additionalProperties':False,'properties':{'nested':{'type':'object','properties':{'tenantId':{'type':'string'}}}}}))}),tf,'/inputSchema/properties/nested/properties/tenantId','POLICY_DENIED')
bad('tool-write-not-idempotent',base({tf:js(tool(sideEffect='write',idempotent=False))}),tf,'/idempotent','SCHEMA_INVALID')
wrong=tool();wrong['schemaSha256']='0'*64
bad('tool-wrong-hash',base({tf:js(wrong)}),tf,'/schemaSha256','HASH_MISMATCH')
bad('manifest-pointer-escape',manifest(base(),surfaces={'skills':'./../outside'}),'.qian-plugin/plugin.json','/surfaces/skills','PATH_INVALID')
bad('manifest-unknown-property',manifest(base(),unknown=True),'.qian-plugin/plugin.json','/unknown','SCHEMA_INVALID')
bad('markdown-link-escape',base({sf:md({'name':'guide','description':DESC},'[外部文件](../../../outside.md)')}),sf,'','PATH_INVALID')
bad('patch-duplicate-identity',base({pf:'- id: files\n  config: {}\n- id: files\n  config: {}\n'}),pf,'/1/id','NAME_MISMATCH')
bad('manifest-symlink',manifest(base(),surfaces={'tools':'./linked'}),'.qian-plugin/plugin.json','/surfaces/tools','PATH_INVALID',symlinks={'linked':'outside'})
bad('inject-provides-overlap',manifest(base(),inject=['Files'],provides=['Files']),'.qian-plugin/plugin.json','/provides','POLICY_DENIED')
bad('yaml-alias',base({sf:'---\nname: guide\ndescription: &d '+DESC+'\nallowed-tools: [*d]\n---\n正文\n'}),sf,'','SYNTAX_INVALID')
case('metadata-json',base({'metadata/permissions.json':js([{'resource':'customer','actions':['read']}]),'metadata/roles.json':js([{'name':'reader','permissions':['customer:read']}])}))
case('metadata-custom-directory',manifest(base({'custom/roles.json':'[]\n'}),surfaces={'metadata':'./custom'}))
case('metadata-root-directory',manifest(base({'roles.json':'[]\n'}),surfaces={'metadata':'./'}))
bad('metadata-pointer-file',manifest(base({'metadata/roles.json':'[]\n'}),surfaces={'metadata':'./metadata/roles.json'}),'.qian-plugin/plugin.json','/surfaces/metadata','PATH_INVALID')
bad('metadata-invalid-json',base({'metadata/roles.json':'{"roles":'}),'metadata/roles.json','','SYNTAX_INVALID')
bad('metadata-duplicate-key',base({'metadata/roles.json':'{"name":"reader","name":"owner"}'}),'metadata/roles.json','/name','SYNTAX_INVALID')
bad('metadata-plaintext-secret',base({'metadata/roles.json':'{"apiKey":"DO-NOT-LEAK"}'}),'metadata/roles.json','/apiKey','SECRET_LITERAL')
bad('metadata-symlink',manifest(base(),surfaces={'metadata':'./linked'}),'.qian-plugin/plugin.json','/surfaces/metadata','PATH_INVALID',symlinks={'linked':'outside'})
# 清单准入（S1-RT-004）：缺任一 S1 必填字段、OCI 引用用 tag、scope 种类非法、工具输入含租户字段、inject 无 Provider。
for field in json.loads((ROOT/'schemas/v2/plugin.schema.json').read_text())['required']:
 value=copy.deepcopy(MANIFEST);value.pop(field)
 bad('manifest-missing-'+field,{'.qian-plugin/plugin.json':js(value)},'.qian-plugin/plugin.json','/'+field,'SCHEMA_INVALID')
workload={**MANIFEST['policy'],'sandbox':'workload'}
image='oci://registry.example.test/qian/fixture'
bad('manifest-oci-tag',manifest(base(),runtime={'form':'service','image':image+':1.0.0'},policy=workload),'.qian-plugin/plugin.json','/runtime/image','SCHEMA_INVALID')
case('manifest-oci-digest',manifest(base(),runtime={'form':'service','image':image+'@sha256:'+'a'*64},policy=workload))
bad('manifest-scope-kind',manifest(base(),scopes=['global','tenant','org']),'.qian-plugin/plugin.json','/scopes/2','SCHEMA_INVALID')
top=tool(inputSchema={'type':'object','additionalProperties':False,'properties':{'tenantId':{'type':'string'}}})
bad('tool-top-level-tenant',base({tf:js(top)}),tf,'/inputSchema/properties/tenantId','SCHEMA_INVALID')
# 工具输入的租户字段（S1-AGT-002）：顶层由 tool.schema.json 的 propertyNames（v0 TenantFieldName）拒绝，嵌套、$defs、数组项与组合子句由语义检查拒绝；
# 两处各自实现同一条名称规则（tenant、tenantId、tenant_id、tenant-id，大小写不敏感），所以每种写法两层都有一例。名称整体匹配，只是含有 tenant 的名称照常通过。
def schema(properties,**extra): return {'type':'object','additionalProperties':False,'properties':properties,**extra}
for suffix,field in [('bare','tenant'),('snake','tenant_id'),('kebab','tenant-id'),('upper','TENANT_ID')]:
 bad('tool-top-level-tenant-'+suffix,base({tf:js(tool(inputSchema=schema({field:{'type':'string'}})))}),tf,'/inputSchema/properties/'+field,'SCHEMA_INVALID')
for suffix,field in [('bare','tenant'),('snake','tenant_id'),('kebab','tenant-id'),('pascal','TenantId')]:
 bad('tool-nested-tenant-'+suffix,base({tf:js(tool(inputSchema=schema({'nested':{'type':'object','properties':{field:{'type':'string'}}}})))}),tf,'/inputSchema/properties/nested/properties/'+field,'POLICY_DENIED')
bad('tool-defs-tenant',base({tf:js(tool(inputSchema=schema({'scope':{'$ref':'#/$defs/scope'}},**{'$defs':{'scope':{'type':'object','properties':{'tenant_id':{'type':'string'}}}}})))}),tf,'/inputSchema/$defs/scope/properties/tenant_id','POLICY_DENIED')
bad('tool-array-items-tenant',base({tf:js(tool(inputSchema=schema({'rows':{'type':'array','items':{'type':'object','properties':{'TenantId':{'type':'string'}}}}})))}),tf,'/inputSchema/properties/rows/items/properties/TenantId','POLICY_DENIED')
bad('tool-any-of-tenant',base({tf:js(tool(inputSchema=schema({'filter':{'anyOf':[{'type':'object','properties':{'tenant':{'type':'string'}}},{'type':'string'}]}})))}),tf,'/inputSchema/properties/filter/anyOf/0/properties/tenant','POLICY_DENIED')
# properties 之外点名租户字段的写法（S1-AGT-004）：两端与能力契约共用同一规则（packages/Common/TenantFieldRule.cs），都由语义检查拒绝。
# 顶层的 required 与 patternProperties 不受 tool Schema 的 propertyNames 约束；模式不加锚点，能匹配任一租户名的任一大小写写法、或超出可移植子集无法判定，都拒绝。
def nested(**extra): return schema({'filter':{'type':'object','properties':{'name':{'type':'string'}},**extra}})
bad('tool-required-tenant',base({tf:js(tool(inputSchema=nested(required=['name','tenantId'])))}),tf,'/inputSchema/properties/filter/required/1','POLICY_DENIED')
bad('tool-top-level-required-tenant',base({tf:js(tool(inputSchema=schema({'message':{'type':'string'}},required=['tenant_id'])))}),tf,'/inputSchema/required/0','POLICY_DENIED')
bad('tool-dependent-required-tenant',base({tf:js(tool(inputSchema=nested(dependentRequired={'name':['tenant-id']})))}),tf,'/inputSchema/properties/filter/dependentRequired/name/0','POLICY_DENIED')
bad('tool-dependent-required-key-tenant',base({tf:js(tool(inputSchema=nested(dependentRequired={'Tenant':['name']})))}),tf,'/inputSchema/properties/filter/dependentRequired/Tenant','POLICY_DENIED')
bad('tool-dependent-schemas-tenant',base({tf:js(tool(inputSchema=nested(dependentSchemas={'TENANT_ID':{'required':['name']}})))}),tf,'/inputSchema/properties/filter/dependentSchemas/TENANT_ID','POLICY_DENIED')
for suffix,pattern in [('prefix','^tenant'),('case','^Tenant(Id)?$'),('wildcard','^[a-z_]+$'),('unsupported',r'\bmeta\b')]:
 bad('tool-pattern-properties-tenant-'+suffix,base({tf:js(tool(inputSchema=nested(patternProperties={pattern:{'type':'string'}})))}),tf,'/inputSchema/properties/filter/patternProperties/'+pattern,'POLICY_DENIED')
bad('tool-top-level-pattern-properties-tenant',base({tf:js(tool(inputSchema=schema({'message':{'type':'string'}},patternProperties={'tenant':{'type':'string'}})))}),tf,'/inputSchema/patternProperties/tenant','POLICY_DENIED')
# 名称表里叫 default 的属性是名称，照常往下查；示例、默认值等实例数据不当 Schema 检查（见对照正例的 notes）。
bad('tool-nested-tenant-keyword-name',base({tf:js(tool(inputSchema=schema({'default':{'type':'object','properties':{'tenant_id':{'type':'string'}}}})))}),tf,'/inputSchema/properties/default/properties/tenant_id','POLICY_DENIED')
# propertyNames 在肯定位置（根与 allOf / anyOf / oneOf / then / else 之下）用 const / enum 列出租户名才算点名（S1-AGT-004 裁决 ②）。
def keyed(names): return schema({'labels':{'type':'object','propertyNames':names,'additionalProperties':{'type':'string'}}})
bad('tool-property-names-tenant-enum',base({tf:js(tool(inputSchema=keyed({'enum':['region','tenant_id']})))}),tf,'/inputSchema/properties/labels/propertyNames/enum/1','POLICY_DENIED')
bad('tool-property-names-tenant-const',base({tf:js(tool(inputSchema=keyed({'anyOf':[{'const':'TenantId'},{'pattern':'^x-'}]})))}),tf,'/inputSchema/properties/labels/propertyNames/anyOf/0/const','POLICY_DENIED')
# 其余肯定位置各一例（S1-AGT-007）：then、else、allOf、oneOf 之下列出的租户名同样算点名，if 之下的条件不算。
bad('tool-property-names-tenant-then',base({tf:js(tool(inputSchema=keyed({'if':{'pattern':'^t'},'then':{'const':'Tenant-Id'}})))}),tf,'/inputSchema/properties/labels/propertyNames/then/const','POLICY_DENIED')
bad('tool-property-names-tenant-else',base({tf:js(tool(inputSchema=keyed({'if':{'pattern':'^x-'},'then':{'maxLength':20},'else':{'enum':['region','tenant']}})))}),tf,'/inputSchema/properties/labels/propertyNames/else/enum/1','POLICY_DENIED')
bad('tool-property-names-tenant-all-of',base({tf:js(tool(inputSchema=keyed({'allOf':[{'pattern':'^[a-z_]+$'},{'enum':['region','tenant_id']}]})))}),tf,'/inputSchema/properties/labels/propertyNames/allOf/1/enum/1','POLICY_DENIED')
bad('tool-property-names-tenant-one-of',base({tf:js(tool(inputSchema=keyed({'oneOf':[{'const':'region'},{'const':'TENANT'}]})))}),tf,'/inputSchema/properties/labels/propertyNames/oneOf/1/const','POLICY_DENIED')
# 旧式 dependencies（S1-AGT-007）：名称、数组项与 Schema 值各一例；Ajv 2020 仍按旧语义执行它。
bad('tool-dependencies-tenant-key',base({tf:js(tool(inputSchema=nested(dependencies={'TenantId':['name']})))}),tf,'/inputSchema/properties/filter/dependencies/TenantId','POLICY_DENIED')
bad('tool-dependencies-tenant-item',base({tf:js(tool(inputSchema=nested(dependencies={'name':['tenant_id']})))}),tf,'/inputSchema/properties/filter/dependencies/name/0','POLICY_DENIED')
bad('tool-dependencies-tenant-schema',base({tf:js(tool(inputSchema=nested(dependencies={'name':{'required':['tenant']}})))}),tf,'/inputSchema/properties/filter/dependencies/name/required/0','POLICY_DENIED')
# 名称表里叫实例关键字的条目照常往下查（S1-AGT-007）：旧式 definitions 与 $defs 各一例；名称表若被当普通成员遍历，这两个条目会被当实例数据跳过。
bad('tool-definitions-tenant-keyword-name',base({tf:js(tool(inputSchema=schema({'message':{'type':'string'}},definitions={'const':{'type':'object','properties':{'tenantId':{'type':'string'}}}})))}),tf,'/inputSchema/definitions/const/properties/tenantId','POLICY_DENIED')
bad('tool-defs-tenant-keyword-name',base({tf:js(tool(inputSchema=schema({'message':{'type':'string'}},**{'$defs':{'examples':{'type':'object','required':['tenant']}}})))}),tf,'/inputSchema/$defs/examples/required/0','POLICY_DENIED')
# 片段为 JSON Pointer 的引用途经实例数据或停在名称表本身上（S1-AGT-007 裁决，用户 2026-10-23 认可）：执行侧按原始 JSON 导航，引用到的数据会被当 Schema 执行，
# 遍历却没有按 Schema 查过；两端都按无法判定拒绝，位置指向引用本身。百分号编码、编码的斜杠（片段中间与开头）、$dynamicRef 与 $id 内嵌资源里的同名路径各一例。
shaped={'type':'object','properties':{'tenantId':{'type':'string'}},'required':['tenantId']}
def referred(target,defs,key='$ref'): return schema({'message':{'type':'string'},'scope':{key:target}},**{'$defs':defs})
bad('tool-ref-tenant-default',base({tf:js(tool(inputSchema=referred('#/$defs/x/default',{'x':{'default':shaped}})))}),tf,'/inputSchema/properties/scope/$ref','POLICY_DENIED')
bad('tool-ref-tenant-percent-encoded',base({tf:js(tool(inputSchema=referred('#/$defs/x/%64efault',{'x':{'default':shaped}})))}),tf,'/inputSchema/properties/scope/$ref','POLICY_DENIED')
bad('tool-ref-tenant-encoded-slash',base({tf:js(tool(inputSchema=referred('#/$defs/x%2Fproperties/default',{'x/properties':{'default':shaped}})))}),tf,'/inputSchema/properties/scope/$ref','POLICY_DENIED')
bad('tool-ref-tenant-encoded-leading-slash',base({tf:js(tool(inputSchema=referred('#%2F$defs%2Fx%2Fdefault',{'x':{'default':shaped}})))}),tf,'/inputSchema/properties/scope/$ref','POLICY_DENIED')
bad('tool-ref-tenant-name-table',base({tf:js(tool(inputSchema=referred('#/$defs/x/properties',{'x':{'type':'object','properties':{'properties':{'tenantId':{'type':'string'}}}}})))}),tf,'/inputSchema/properties/scope/$ref','POLICY_DENIED')
bad('tool-dynamic-ref-tenant',base({tf:js(tool(inputSchema=referred('#/$defs/x/const',{'x':{'const':shaped}},'$dynamicRef')))}),tf,'/inputSchema/properties/scope/$dynamicRef','POLICY_DENIED')
shadow=schema({'scope':{'$id':'scope.json','type':'object','$defs':{'x':{'default':shaped}},'properties':{'value':{'$ref':'#/$defs/x/default'}}}},**{'$defs':{'x':{'default':{'type':'object'}}}})
bad('tool-ref-tenant-id-shadow',base({tf:js(tool(inputSchema=shadow))}),tf,'/inputSchema/properties/scope/properties/value/$ref','POLICY_DENIED')
# 对照正例：只是含有 tenant 的名称、tenant 后接换行（TenantFieldName 整体匹配）、不能匹配租户名的模式、实例数据里形如 Schema 的租户字段
# （default、const、enum、example、examples 各一处，哪个被当 Schema 检查都会误报），以及开放的映射（裁决 ①）、propertyNames 只收紧格式的 pattern
# 与 not 之下的排除（裁决 ②），都照常通过；途经名称表里叫实例关键字的条目、停在 Schema 位置上的引用与锚点引用也照常通过（S1-AGT-007）。
lookalike={'type':'object','properties':{'tenants':{'type':'string'},'tenant\n':{'type':'string'}},'required':['tenants'],'patternProperties':{'^x-':{},'^tenants$':{},'^lieutenant':{}},'dependentRequired':{'tenants':['tenant\n']}}
notes={'type':'object','default':shaped,'const':shaped,'enum':[shaped],'example':shaped,'examples':[shaped]}
labels={'type':'object','propertyNames':{'pattern':'^[a-z]+$','not':{'enum':['tenant','tenantId','tenant_id','tenant-id']}},'additionalProperties':{'type':'string'}}
refs={'type':'object','properties':{'whole':{'$ref':'#/$defs/default'},'part':{'$ref':'#/$defs/default/properties/enum'},'anchored':{'$ref':'#keyed'}}}
named={'default':{'$anchor':'keyed','type':'object','properties':{'enum':{'type':'string'}}}}
case('tool-tenant-lookalike',base({tf:js(tool(inputSchema=schema({'lieutenant':{'type':'string'},'lieutenant_id':{'type':'string'},'nested':lookalike,'notes':notes,'labels':labels,'refs':refs,'metadata':{'type':'object','additionalProperties':{'type':'string'}}},**{'$defs':named})))}))
# propertyNames 不合格时两侧都指向出错的属性名本身。
bad('mcp-invalid-server-name',base({mf:js({'mcpServers':{'Bad_Name':{'type':'http','url':'https://api.example.test/mcp'}}})}),mf,'/mcpServers/Bad_Name','SCHEMA_INVALID')
keys={'serviceKeys':['Data','Registry']}
bad('inject-without-provider',manifest(base(),inject=['Data','Files']),'.qian-plugin/plugin.json','/inject/1','REFERENCE_UNKNOWN',keys)
case('inject-with-provider',manifest(base(),inject=['Data']),options=keys)
case('inject-unchecked-without-catalog',manifest(base(),inject=['Files']))
# 未成对的代理项（S1-RT-022）：JSON 与 YAML 里的这类转义两端同样拒绝，致命错误只报它自己；转义的代理对照常接受。U 是文本里反斜杠加 u 的转义前缀。
U=chr(92)+'u'
bad('metadata-unpaired-surrogate-value',base({'metadata/roles.json':'[\n  {\n    "name": "reader'+U+'d800"\n  }\n]\n'}),'metadata/roles.json','','SYNTAX_INVALID')
bad('metadata-unpaired-surrogate-key',base({'metadata/roles.json':'{\n  "name": "reader",\n  "'+U+'dc00": 1\n}\n'}),'metadata/roles.json','','SYNTAX_INVALID')
bad('metadata-unpaired-surrogate-after-duplicate',base({'metadata/roles.json':'{\n  "name": "reader",\n  "name": "owner",\n  "title": "'+U+'d800"\n}\n'}),'metadata/roles.json','','SYNTAX_INVALID')
bad('patch-unpaired-surrogate',base({pf:'- id: files\n  config:\n    title: "x'+U+'d800"\n'}),pf,'','SYNTAX_INVALID')
bad('patch-unpaired-surrogate-after-duplicate',base({pf:'- id: files\n  config:\n    a: 1\n    a: 2\n    title: "'+U+'dc00"\n'}),pf,'','SYNTAX_INVALID')
bad('patch-alias-before-unpaired-surrogate',base({pf:'- id: files\n  config:\n    a: &x 1\n    b: *x\n    title: "'+U+'d800"\n'}),pf,'','SYNTAX_INVALID')
bad('patch-double-high-surrogate',base({pf:'- id: files\n  config:\n    title: "'+U+'d83d'+U+'d83d'+U+'de00"\n'}),pf,'','SYNTAX_INVALID')
bad('patch-unclosed-flow',base({pf:'- id: files\n  config: [\n'}),pf,'','SYNTAX_INVALID')
bad('skill-unpaired-surrogate',base({sf:'---\nname: guide\ndescription: "'+DESC+U+'d800"\n---\n\n正文\n'}),sf,'','SYNTAX_INVALID')
case('escaped-surrogate-pairs',base({sf:'---\nname: guide\ndescription: "'+DESC+U+'d83d'+U+'de00"\n---\n\n正文\n','metadata/roles.json':'[\n  {\n    "name": "reader'+U+'d83d'+U+'de00"\n  }\n]\n'}))
# 解析诊断（S1-RT-025）：期望写明行号，约定见 packages/Qian.Surfaces/README.md；文案由 syntax-vectors.json 逐字比较。
bad('patch-tab-indentation',base({pf:'- id: files\n  config:\n\tquota: 100\n'}),pf,'','SYNTAX_INVALID',line=3)
bad('patch-unclosed-quote',base({pf:'- id: files\n  config:\n    title: "x\n    quota: 100\n'}),pf,'','SYNTAX_INVALID',line=3)
bad('patch-second-document',base({pf:'- id: files\n  config: {}\n---\n- id: other\n  config: {}\n'}),pf,'','SYNTAX_INVALID',line=3)
bad('patch-directive',base({pf:'%YAML 1.2\n---\n- id: files\n  config: {}\n'}),pf,'','SYNTAX_INVALID',line=1)
bad('patch-empty-key',base({pf:'- id: files\n  config:\n    : 1\n    quota: 100\n'}),pf,'','SYNTAX_INVALID',line=3)
bad('patch-duplicate-outer-before-nested',base({pf:'- id: files\n  config:\n    a: 1\n  config:\n    b: 1\n    b: 2\n'}),pf,'','SYNTAX_INVALID',line=4)
bad('skill-front-matter-tab',base({sf:'---\nname: guide\n\tdescription: '+DESC+'\n---\n\n正文\n'}),sf,'','SYNTAX_INVALID',line=3)
case('skill-block-scalar-at-end',base({sf:'---\nname: guide\ndescription: |\n  '+DESC+'\n---\n\n正文\n'}))
case('metadata-json-depth-64',base({'metadata/roles.json':'['*64+']'*64+'\n'}))
bad('metadata-json-depth-65',base({'metadata/roles.json':'['*65+']'*65+'\n'}),'metadata/roles.json','','SYNTAX_INVALID',line=1)
bad('metadata-non-finite',base({'metadata/roles.json':'{\n  "limit": 1e999\n}\n'}),'metadata/roles.json','','SYNTAX_INVALID',line=1)
case('metadata-json-bom',base({'metadata/roles.json':chr(0xfeff)+js([{'name':'reader'}])}))
# UI v2（S1-YUE-001）：结构与静态语义同一组向量供 TS/.NET 读取；浏览器 SDK 也消费这些向量，
# uiRuntimeValid 明确区分静态可索引描述与当前 MF2 加载支持的子集，不放宽任何表面校验。
UI_BASE = {
 '$schema':'https://qian.dev/schemas/ui/v2',
 'federation':{'manifest':'/remotes/example/mf-manifest.json','exposes':['Page','Widget','Settings']},
 'engines':{'yue':'^0.1.0'},
 'pages':[{'route':'/example','expose':'Page','permission':'qian.example:item:read','nav':{'title':'示例','icon':'folder','order':1}}]
}
UI_BRIDGE = {
 'slots':['qian.shell.nav/v1','qian.shell.client-action/v1'],'navigate':['/example'],'notify':False,
 'capabilities':[{'id':'qian.example/save','permission':'qian.example:item:write','sideEffect':'write'}]
}
UI_ACTION = {
 'slot':'qian.shell.client-action/v1','id':'save','title':'保存','description':'保存当前内容',
 'permission':'qian.example:item:write','expose':'Page',
 'parameters':{'type':'object','additionalProperties':False,'properties':{}}
}
UI_FULL = copy.deepcopy(UI_BASE)
UI_FULL.update(bridge=UI_BRIDGE,contributions=[UI_ACTION])

def ui_case(name, data, valid=True, pointer=None, code='SCHEMA_INVALID', runtime=None, file='ui/manifest.json', declared=False):
 files=base({file:js(data)})
 if declared: manifest(files,surfaces={'ui':'./'+file})
 case('ui-'+name,files,valid,[] if pointer is None else [issue(file,pointer,code)])
 CASES[-1].update(uiFile=file,uiRuntimeValid=valid if runtime is None else runtime)

def ui_bad(name, edit, pointer, code='SCHEMA_INVALID', source=UI_FULL):
 data=copy.deepcopy(source);edit(data)
 ui_case(name,data,False,pointer,code)

ui_case('legacy-pages',UI_BASE,runtime=False)
UI_WIDGETS=copy.deepcopy(UI_BASE)
UI_WIDGETS['widgets']=[{'type':'qian.file','expose':'Widget'}]
UI_WIDGETS['settingsCards']=[{'expose':'Settings','scope':'tenant','permission':'qian.example:settings:write'}]
ui_case('spec-widgets-settings',UI_WIDGETS,runtime=False)
ui_case('bridge-contributions',UI_FULL)
ui_case('declared-custom-path',UI_FULL,file='frontend/entry.json',declared=True)
ui_bad('missing-engine',lambda d:d['engines'].pop('yue'),'/engines/yue')
ui_bad('unknown-field',lambda d:d.update(hidden=True),'/hidden')
ui_bad('unknown-nav-field',lambda d:d['pages'][0]['nav'].update(hidden=True),'/pages/0/nav/hidden')
ui_bad('bad-schema',lambda d:d.update({'$schema':'https://qian.dev/schemas/ui/v99'}),'/$schema')
ui_bad('empty-exposes',lambda d:d['federation'].update(exposes=[]),'/federation/exposes')
ui_bad('duplicate-exposes',lambda d:d['federation']['exposes'].append('Page'),'/federation/exposes')
ui_bad('unknown-page-expose',lambda d:d['pages'][0].update(expose='Missing'),'/pages/0/expose','REFERENCE_UNKNOWN')
ui_bad('duplicate-page-route',lambda d:d['pages'].append(copy.deepcopy(d['pages'][0])),'/pages/1/route','NAME_MISMATCH')
ui_bad('parent-route',lambda d:d['pages'][0].update(route='/safe/../escape'),'/pages/0/route')
ui_bad('space-route',lambda d:d['pages'][0].update(route='/bad path'),'/pages/0/route')
ui_bad('missing-bridge',lambda d:d.pop('bridge'),'/bridge')
ui_bad('unknown-slot',lambda d:d['contributions'][0].update(slot='qian.shell.unknown/v1'),'/contributions/0/slot')
ui_bad('missing-action-parameters',lambda d:d['contributions'][0].pop('parameters'),'/contributions/0/parameters')
ui_bad('bad-action-schema',lambda d:d['contributions'][0].update(parameters={'type':99}),'/contributions/0/parameters/type')
ui_bad('unknown-contribution-field',lambda d:d['contributions'][0].update(hidden=True),'/contributions/0/hidden')
ui_bad('unallowed-slot',lambda d:d['bridge'].update(slots=['qian.shell.nav/v1']),'/contributions/0/slot','POLICY_DENIED')
ui_bad('unallowed-page-nav',lambda d:d['bridge'].update(slots=['qian.shell.client-action/v1']),'/pages/0','POLICY_DENIED')
ui_bad('duplicate-contribution',lambda d:d['contributions'].append(copy.deepcopy(d['contributions'][0])),'/contributions/1/id','NAME_MISMATCH')
ui_bad('duplicate-bridge-capability',lambda d:d['bridge']['capabilities'].append(copy.deepcopy(d['bridge']['capabilities'][0])),'/bridge/capabilities/1/id','NAME_MISMATCH')
ui_bad('unknown-widget-field',lambda d:d['widgets'][0].update(hidden=True),'/widgets/0/hidden',source=UI_WIDGETS)
ui_bad('unknown-widget-expose',lambda d:d['widgets'][0].update(expose='Missing'),'/widgets/0/expose','REFERENCE_UNKNOWN',source=UI_WIDGETS)
ui_bad('duplicate-widget-type',lambda d:d['widgets'].append(copy.deepcopy(d['widgets'][0])),'/widgets/1/type','NAME_MISMATCH',source=UI_WIDGETS)
ui_bad('missing-settings-permission',lambda d:d['settingsCards'][0].pop('permission'),'/settingsCards/0/permission',source=UI_WIDGETS)
ui_bad('duplicate-settings-card',lambda d:d['settingsCards'].append(copy.deepcopy(d['settingsCards'][0])),'/settingsCards/1/expose','NAME_MISMATCH',source=UI_WIDGETS)

(ROOT/'tck/surfaces/cases.json').write_text(js(CASES).translate(INVISIBLE))
# 文字规范值独立于生产实现，用于跨语言验证浮点、负零、Unicode 与 UTF-16 排序。
vectors=[]
for name,source,canonical in [
 ('numbers','{"name":"n","description":"numbers","inputSchema":{"a":333333333.33333329,"b":1e-7,"c":1e-6,"d":1e21,"e":1e20,"f":-0,"g":5e-324}}','{"description":"numbers","inputSchema":{"a":333333333.3333333,"b":1e-7,"c":0.000001,"d":1e+21,"e":100000000000000000000,"f":0,"g":5e-324},"name":"n"}'),
 ('unicode','{"name":"u","description":"中文\\n正文","inputSchema":{"\\ufffd":1,"😀":2,"a":3}}','{"description":"中文\\n正文","inputSchema":{"a":3,"😀":2,"�":1},"name":"u"}')]:
 vectors.append({'name':name,'source':source,'canonical':canonical,'sha256':hashlib.sha256(canonical.encode()).hexdigest()})
(ROOT/'tck/surfaces/jcs-vectors.json').write_text(js(vectors))
# 解析诊断向量（S1-RT-025）：两端直接调用 Parse，期望恰好一条文件根诊断（SURFACE_SYNTAX_INVALID），行号与文案逐字相同。
YS='YAML 语法无效或存在重复键。';GEN='文件语法、大小、嵌套深度或值无效。';DOCS='YAML 只能包含一个文档。';JSYN='JSON 语法无效。'
CHARS='YAML 不能含 NUL、单独的回车符、U+0085、U+2028 或 U+2029。';DIRECTIVE='不支持 YAML 指令（以 % 开头的行）。'
SYNTAX=[];J='values.json';B=chr(92)
def syntax(name,source,line,message,file='values.yml'): SYNTAX.append({'name':name,'file':file,'markdown':file.endswith('.md'),'source':source,'line':line,'message':message})
def front(name,matter,line,message): syntax(name,'---\n'+matter+'---\n\n正文\n',line,message,'guide.md')
# 引号标量的错误（未闭合、内部转义无效、续行缩进不足）报在标量开始的行。
syntax('yaml-unclosed-double-quote','a: 1\nb: "x\n  y\n',2,YS)
syntax('yaml-unclosed-single-quote',"a: 1\nb: 'x\n  y\n",2,YS)
syntax('yaml-unclosed-quote-without-final-newline','a: 1\nb: "x',2,YS)
syntax('yaml-invalid-escape-inside-quote','a: 1\nb: "x\n  '+B+'q"\n',2,YS)
syntax('yaml-underindented-double-quote','a:\n  b: "x\n  y"\n',2,YS)
syntax('yaml-underindented-single-quote',"a:\n  b: 'x\n  y'\n",2,YS)
syntax('yaml-underindented-quote-after-escaped-quote','a:\n  b: "x'+B+'"\n  y"\n',2,YS)
syntax('yaml-escaped-line-break-before-empty-line','a: "x'+B+'\n\n  y"\n',1,YS)
# 到输入末尾仍未闭合的流式集合报在末尾所在的行（末尾没有换行时先补一个）；流式集合的续行缩进不足报在该续行。
syntax('yaml-unclosed-flow-sequence','a: [1,\n  2,\n',3,YS)
syntax('yaml-unclosed-flow-without-final-newline','a: [1,\n  2,',3,YS)
syntax('yaml-unclosed-flow-mapping','a: {b: 1,\n  c: 2\n',3,YS)
syntax('yaml-underindented-flow','a:\n  b: [1,\n  2]\n',3,YS)
syntax('yaml-nested-flow-closing-at-indent','a:\n  b: [[1,\n    2\n  ]]\n',4,YS)
# 行首缩进里的制表符报在所在行；多行隐式键报在冒号所在行，键在冒号之前已结束时报在键开始的行。
syntax('yaml-tab-indentation','a:\n\tb: 1\n',2,YS)
syntax('yaml-tab-after-indentation-spaces','a:\n  b: 1\n  \tc: 2\n',3,YS)
syntax('yaml-tab-after-dash-before-tag','- \t!!str a\n',1,YS)
syntax('yaml-tab-after-dash-before-compact-mapping','- \ta: 1\n',1,YS)
syntax('yaml-multiline-plain-key','a: 1\nb\n c: 2\n',3,YS)
syntax('yaml-plain-key-ended-by-indentation','x:\n  a: 1\n  b\n  c: 2\n',3,YS)
syntax('yaml-multiline-quoted-first-key','"b\n c\n d": 2\n',3,YS)
syntax('yaml-multiline-quoted-later-key','a: 1\n"b\n c": 2\n',2,YS)
syntax('yaml-colon-on-next-line','a: 1\nkey\n : value\n',3,YS)
syntax('yaml-colon-on-next-line-after-quoted-key','a: 1\n"key"\n : value\n',2,YS)
# 逐行预检先于解析：第二个文档、指令行与禁用字符报在所在行。
syntax('yaml-second-document','a: 1\n---\nb: 2\n',2,DOCS)
syntax('yaml-content-after-document-end','a: 1\n...\nb: 2\n',3,DOCS)
syntax('yaml-second-document-before-its-errors','a: 1\n---\nb: "x\n',2,DOCS)
syntax('yaml-document-marker-inside-quote','a: "x\n---\nb: 1\n',2,DOCS)
syntax('yaml-directive','%YAML 1.2\n---\na: 1\n',1,DIRECTIVE)
syntax('yaml-directive-after-content','a: 1\n%TAG ! x\n',2,DIRECTIVE)
syntax('yaml-nul','a: 1\nb: x'+chr(0)+'\n',2,CHARS)
syntax('yaml-lone-carriage-return','a: 1\rb: 2\n',1,CHARS)
syntax('yaml-next-line','a: 1\nb: x'+chr(0x85)+'\n',2,CHARS)
syntax('yaml-line-separator','a: 1\nb: x'+chr(0x2028)+'\n',2,CHARS)
# 重复键按名称比较，报文中最先出现的那个；alias 在整篇无语法错误后才报，先于重复键。
syntax('yaml-duplicate-nested-before-outer','a:\n  x: 1\n  x: 2\na: 3\n',3,YS)
syntax('yaml-duplicate-outer-before-nested','a: 1\na:\n  x: 1\n  x: 2\n',2,YS)
syntax('yaml-duplicate-quoted-and-plain','1: a\n"1": b\n',2,YS)
syntax('yaml-duplicate-empty-keys-in-flow','{: 1, : 2}\n',1,YS)
# 块映射里的空键两端都拒绝（YamlDotNet 18.1.0 在嵌套块映射里会吞掉其后的值），报在条目开始的行。
syntax('yaml-empty-key','a: 1\n: 2\n',2,YS)
syntax('yaml-nested-empty-key','x:\n  : 1\n  b: 2\n',2,YS)
syntax('yaml-explicit-empty-key','? \n: 1\n',1,YS)
syntax('yaml-alias','a: &x 1\nb: *x\n',1,GEN)
syntax('yaml-syntax-error-precedes-alias','a: &x 1\nb: *x\nc: "x\n',3,YS)
syntax('yaml-alias-precedes-duplicate','a: &x 1\na: *x\n',1,GEN)
syntax('yaml-alias-key-before-colon','a: &x 1\n*x: 2\n',2,YS)
# 过深、值无效与 alias 用通用文案，一律报第 1 行。
syntax('yaml-flow-depth-65','['*65+']'*65+'\n',1,GEN)
syntax('yaml-block-depth-65','- '*65+'x\n',1,GEN)
syntax('yaml-explicit-int-mismatch','a: !!int x\n',1,GEN)
syntax('yaml-explicit-int-trailing-newline','a: !!int "12'+B+'n"\n',1,GEN)
syntax('yaml-custom-tag','a: !custom x\n',1,GEN)
syntax('yaml-infinity','a: .inf\n',1,GEN)
syntax('yaml-bom-keeps-lines',chr(0xfeff)+'a: 1\nb: "x\n',2,YS)
# YamlDotNet 18.1.0 比规范严的几处，CLI 同样拒绝。
syntax('yaml-literal-first-line-only-spaces','a: |\n  \n  x\n',3,YS)
syntax('yaml-folded-leading-blank-lines','a: >\n\n  \n',4,YS)
syntax('yaml-flow-value-then-colon','a: []: b\n',1,YS)
syntax('yaml-anchored-value-in-flow-mapping','a: {x: &a 1, y: 2}\n',1,YS)
# front matter 的行号从文件第 1 行（开头的 ---）算起；其末尾在结束分隔符所在的行。
front('front-matter-tab-indentation','name: guide\n\tdescription: x\n',3,YS)
front('front-matter-content-after-document-end','name: guide\n...\ndescription: x\n',4,DOCS)
front('front-matter-unclosed-flow','name: guide\ndescription: [x\n',4,YS)
# JSON：语法错误、第 65 层容器与非有限数值按文中顺序先到先报；数值要读到其后的分隔符才算读完。
syntax('json-non-finite','{\n  "a": 1e999\n}\n',1,GEN,J)
syntax('json-non-finite-precedes-later-syntax-error','{\n  "a": 1e999,\n  "b": \n}\n',1,GEN,J)
syntax('json-syntax-error-precedes-later-non-finite','{\n  "a": tru,\n  "b": 1e999\n}\n',2,JSYN,J)
syntax('json-non-finite-at-end-of-input','{\n  "a": 1e999',2,JSYN,J)
syntax('json-non-finite-root-at-end-of-input','1e999',1,GEN,J)
syntax('json-non-finite-before-invalid-character','{\n  "a": 1e999x\n}\n',2,JSYN,J)
syntax('json-non-finite-before-comment','{\n  "a": 1e999/* c */\n}\n',1,GEN,J)
syntax('json-non-finite-before-extra-bracket','{\n  "a": 1e999]\n',1,GEN,J)
syntax('json-depth-65','['*65+']'*65+'\n',1,GEN,J)
syntax('json-depth-65-empty-object','['*64+'{}'+']'*64+'\n',1,GEN,J)
syntax('json-syntax-error-precedes-depth','[\n  tru,\n  '+'['*65+']'*65+'\n]\n',2,JSYN,J)
syntax('json-depth-precedes-syntax-error','[\n  '+'['*65+'\n  tru\n',1,GEN,J)
syntax('json-trailing-content','{}\n{}\n',2,JSYN,J)
syntax('json-trailing-content-precedes-surrogate','{"a": "'+U+'d800"}\nx\n',2,JSYN,J)
syntax('json-unclosed-at-end-of-input','{"a":\n',2,JSYN,J)
syntax('json-bom-keeps-lines',chr(0xfeff)+'{\n  "a": tru\n}\n',2,JSYN,J)
(ROOT/'tck/surfaces/syntax-vectors.json').write_text(js(SYNTAX).translate(INVISIBLE))
print(f'{len(CASES)} cases: {sum(not c["valid"] for c in CASES)} invalid; {len(SYNTAX)} syntax vectors')
