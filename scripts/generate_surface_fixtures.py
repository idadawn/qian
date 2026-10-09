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
def issue(file,pointer,code): return {'file':file,'pointer':file+'#'+pointer,'code':'SURFACE_'+code}
def bad(name,files,file,pointer,code,options=None,symlinks=None):case(name,files,False,[issue(file,pointer,code)],options,symlinks)
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
(ROOT/'tck/surfaces/cases.json').write_text(js(CASES))
# 文字规范值独立于生产实现，用于跨语言验证浮点、负零、Unicode 与 UTF-16 排序。
vectors=[]
for name,source,canonical in [
 ('numbers','{"name":"n","description":"numbers","inputSchema":{"a":333333333.33333329,"b":1e-7,"c":1e-6,"d":1e21,"e":1e20,"f":-0,"g":5e-324}}','{"description":"numbers","inputSchema":{"a":333333333.3333333,"b":1e-7,"c":0.000001,"d":1e+21,"e":100000000000000000000,"f":0,"g":5e-324},"name":"n"}'),
 ('unicode','{"name":"u","description":"中文\\n正文","inputSchema":{"\\ufffd":1,"😀":2,"a":3}}','{"description":"中文\\n正文","inputSchema":{"a":3,"😀":2,"�":1},"name":"u"}')]:
 vectors.append({'name':name,'source':source,'canonical':canonical,'sha256':hashlib.sha256(canonical.encode()).hexdigest()})
(ROOT/'tck/surfaces/jcs-vectors.json').write_text(js(vectors))
print(f'{len(CASES)} cases: {sum(not c["valid"] for c in CASES)} invalid')
