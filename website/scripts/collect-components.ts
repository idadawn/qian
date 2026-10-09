// 第 2 部分「组件目录」的生成器（docs-site.md §2、§3）。
// S1-DOCS-002 之前仓库里还没有组件文件夹：本脚本先按 PRD §7 与组件模型规格 §7.7 的规划清单生成占位页，
// 让首页与其他部分的链接有落点、目录结构先定下来。S1-DOCS-002 把数据源换成各组件文件夹（README 契约 + docs/*.mdx），
// 页面改为 §3 的九节模板；生成目录不进 Git，也不接受手改。
import fs from 'node:fs';
import path from 'node:path';
import { REPO_ROOT, repoFileDate, yamlString, type GeneratedFile } from './repo.ts';
import { designRoute } from './sync-specs.ts';

type Stage = 'S1' | 'S2' | 'S3' | 'S4';

export interface PlannedComponent {
  id: string;
  name: string;
  stage: Stage;
  /** 一句话用途，取自 PRD 对应小节或 docs-site.md §2a.2。 */
  summary: string;
  /** 设计依据：仓库内 docs/ 下的文件，生成时换成 /design/ 路由。 */
  refs: Array<{ text: string; doc: string }>;
}

const S1_PRD = 'docs/prd/07-s1-foundation.md';
/** 占位页的内容来自本脚本的规划清单，updated 取本脚本的提交日期（S1-DOCS-002 换成组件文件夹后改取各组件文档的日期）。 */
const SELF = 'website/scripts/collect-components.ts';
const SPEC = 'docs/prd/specs/component-model.md';

export const PLATFORM_COMPONENTS: PlannedComponent[] = [
  { id: 'qian.registry', name: '注册表', stage: 'S1', summary: '平台插件三件套之一：清单解析与校验、注册表、租户 scope patch 持久化、修订保留与回切记录；只进 platform profile。', refs: [{ text: 'PRD §7.2', doc: S1_PRD }, { text: '组件模型规格 §7.7、§9.5', doc: SPEC }] },
  { id: 'qian.lifecycle', name: '生命周期', stage: 'S1', summary: '平台插件三件套之一：制品 → 修订 → 激活与回切，租户级开通、升级、卸载，强制验签。', refs: [{ text: 'PRD §7.2', doc: S1_PRD }, { text: '组件模型规格 §5.6', doc: SPEC }] },
  { id: 'qian.capabilities', name: '能力目录', stage: 'S1', summary: '平台插件三件套之一：命令 / 查询 / 事件 / 实体四类能力目录、扩展点与契约仓库、OpenAPI 聚合。', refs: [{ text: 'PRD §7.2', doc: S1_PRD }] },
  { id: 'qian.hooks', name: '钩子', stage: 'S1', summary: '把组件 hooks.json 里的 command / http 监听器转成同模式的进程内监听器。', refs: [{ text: '组件模型规格 §4.4、§5.4', doc: SPEC }] },
  { id: 'qian.tenancy', name: '租户目录与生命周期', stage: 'S1', summary: '租户主档、隔离策略装配（默认共享库 + tenant_id + RLS）、显式状态机与开通编排。', refs: [{ text: 'PRD §7.3', doc: S1_PRD }, { text: 'ADR-004', doc: 'docs/adr/ADR-004-database-tenant-isolation.md' }] },
  { id: 'qian.identity', name: '身份与 SSO', stage: 'S1', summary: 'OIDC / OAuth2 端点、Person + Membership 双层账号、统一登录流水线，连接器 oidc / wecom。', refs: [{ text: 'PRD §7.4', doc: S1_PRD }, { text: 'ADR-005', doc: 'docs/adr/ADR-005-identity-sso.md' }] },
  { id: 'qian.authz', name: '鉴权引擎', stage: 'S1', summary: '权限点注册表、租户域 RBAC 与插件默认角色模板。', refs: [{ text: 'PRD §7.5', doc: S1_PRD }, { text: 'ADR-006', doc: 'docs/adr/ADR-006-authorization-model.md' }] },
  { id: 'qian.data', name: '数据存储默认安全装配', stage: 'S1', summary: 'RLS 拦截器、租户与软删过滤、审计字段、并发令牌与同库 Outbox 的默认装配。', refs: [{ text: 'PRD §7.6', doc: S1_PRD }, { text: 'ADR-004', doc: 'docs/adr/ADR-004-database-tenant-isolation.md' }] },
  { id: 'qian.files', name: '文件存储', stage: 'S1', summary: '租户前缀与配额、预签名上传下载、强制附件下载；对象存储 / S3 兼容 / 本地三种 Provider。', refs: [{ text: 'PRD §7.7', doc: S1_PRD }, { text: '组件模型规格 §6.2', doc: SPEC }] },
  { id: 'qian.config', name: '配置中心', stage: 'S1', summary: 'Apollo Provider（长轮询、访问密钥签名、本地缓存）、配置源顺序与先校验后生效的热更新。', refs: [{ text: 'PRD §7.8', doc: S1_PRD }, { text: 'ADR-007', doc: 'docs/adr/ADR-007-config-center-apollo.md' }] },
  { id: 'qian.gateway', name: '业务 API 网关', stage: 'S1', summary: '网关防护：强制认证、路由级限流、可信代理、出网 SSRF 基线。', refs: [{ text: 'PRD §7.9', doc: S1_PRD }, { text: 'ADR-010', doc: 'docs/adr/ADR-010-gateway-layers.md' }] },
  { id: 'qian.events', name: '事件总线', stage: 'S1', summary: 'Outbox 同事务写入与可靠发布、至少一次消费、幂等表、死信与租户回灌。', refs: [{ text: 'PRD §7.10', doc: S1_PRD }, { text: 'ADR-008', doc: 'docs/adr/ADR-008-events-messaging.md' }] },
  { id: 'qian.audit', name: '审计', stage: 'S1', summary: '命令级哈希链审计与实体级同事务审计，actor 含 agent。', refs: [{ text: 'PRD §7.11', doc: S1_PRD }, { text: 'ADR-009', doc: 'docs/adr/ADR-009-audit.md' }] },
  { id: 'qian.observability', name: '可观测', stage: 'S1', summary: 'OTel、租户 / 用户 / 插件 baggage 与计量事件。', refs: [{ text: 'PRD §7.11', doc: S1_PRD }] },
  { id: 'qian.notify', name: '通知', stage: 'S1', summary: '消息类型注册、模板与发送配置、异步投递，首批三个渠道。', refs: [{ text: 'PRD §7.12', doc: S1_PRD }] },
  { id: 'qian.scheduler', name: '调度', stage: 'S1', summary: 'PG 持久化的作业与触发器、集群安全领取、按业务键可取消。', refs: [{ text: 'PRD §7.13', doc: S1_PRD }] },
  { id: 'qian.realtime', name: '实时推送', stage: 'S1', summary: '可选（P1）：WebSocket / SSE 会话、一次性握手票据、Redis 背板扇出与在线状态。', refs: [{ text: 'PRD §7.13', doc: S1_PRD }] },
  { id: 'qian.entitlement', name: '授权 v0', stage: 'S1', summary: '「租户 × 插件 × 计划」授权表、运营手工授予与权益查询 API。', refs: [{ text: 'PRD §7.14', doc: S1_PRD }, { text: 'ADR-014 D14.5', doc: 'docs/adr/ADR-014-marketplace.md' }] },
  { id: 'qian.shell', name: '前端外壳', stage: 'S1', summary: 'yue 外壳静态制品与外壳 API：MF2 宿主、共享依赖与版本化插槽注册表。', refs: [{ text: 'PRD §7.15', doc: S1_PRD }, { text: 'ADR-013', doc: 'docs/adr/ADR-013-frontend-yue.md' }] },
  { id: 'qian.model-gateway', name: '模型服务网关底座', stage: 'S1', summary: '可选（P1）：GPUStack 注册为 Higress 上游、Provider Adapter、逻辑模型目录。', refs: [{ text: 'PRD §7.16', doc: S1_PRD }, { text: 'ADR-015', doc: 'docs/adr/ADR-015-model-gateway.md' }] },
  { id: 'qian.docs', name: '文档生成器', stage: 'S1', summary: '开发态组件：qian docs generate 从代码生成配置目录、模块图与事件图。', refs: [{ text: '组件模型规格 §7.7', doc: SPEC }, { text: 'ADR-020 D20.9', doc: 'docs/adr/ADR-020-component-model-v2.md' }] },
  { id: 'qian.workflow', name: '流程与自动化', stage: 'S2', summary: '审批内核与触发器：一套 JSON 定义、节点树视图设计器。', refs: [{ text: 'ADR-012', doc: 'docs/adr/ADR-012-workflow-engine.md' }, { text: 'PRD §8', doc: 'docs/prd/08-s2-lowcode.md' }] },
];

export const YUE_PACKAGES: Array<{ name: string; stage: Stage; summary: string }> = [
  { name: '@yue/tokens', stage: 'S1', summary: '三层设计令牌（primitive → semantic → component）、明暗两态与首帧防闪烁。' },
  { name: '@yue/core', stage: 'S1', summary: '约 30 个基础组件：按钮、输入、选择、弹层、反馈、导航等。' },
  { name: '@yue/shell', stage: 'S1', summary: 'AppShell 单一侧栏布局、页面骨架、租户切换器与兜底页。' },
  { name: '@yue/data', stage: 'S1', summary: 'DataTable 基础版、树与级联、日期时间、分片上传。' },
  { name: '@yue/form', stage: 'S1', summary: 'SchemaForm 基础版、校验与基础联动、弹层 API。' },
  { name: '@yue/biz', stage: 'S1', summary: '业务选择器（字典、组织、用户、角色）、菜单与按钮授权树。' },
  { name: '@yue/sdk', stage: 'S1', summary: '注册表 API、PlatformClient、请求拦截器链与 i18n。' },
];

const PLACEHOLDER_NOTE =
  '> 占位页：组件文件夹尚未建立。S1-DOCS-002 起本页由收集脚本从组件文件夹生成，按[组件页模板](/design/prd/specs/docs-site)固定九节：用途、安装、配置、表面、API、示例、模型体验、已知限制与延期项、变更。';

function refLinks(refs: PlannedComponent['refs'], repoRoot: string): string {
  return refs
    .map((r) => {
      if (!fs.existsSync(path.join(repoRoot, r.doc))) throw new Error(`组件目录引用的文档不存在：${r.doc}`);
      return `- [${r.text}](${designRoute(r.doc)})`;
    })
    .join('\n');
}

function componentPage(c: PlannedComponent, repoRoot: string): string {
  return [
    '---',
    `title: ${yamlString(`${c.id} ${c.name}`)}`,
    `description: ${yamlString(c.summary)}`,
    'part: components',
    'project: qian',
    'audience: [developer, operator]',
    `updated: ${repoFileDate(SELF, repoRoot)}`,
    `component: ${c.id}`,
    `stage: ${c.stage}`,
    // S1 是当前阶段不另标；更后阶段在侧栏里显示阶段标记（docs-site.md §2a：规划中的能力打阶段标记）
    ...(c.stage === 'S1' ? [] : [`tag: ${c.stage}`]),
    '---',
    '',
    `# ${c.id} ${c.name}`,
    '',
    PLACEHOLDER_NOTE,
    '',
    `**阶段**：${c.stage}`,
    '',
    c.summary,
    '',
    '## 设计依据',
    '',
    refLinks(c.refs, repoRoot),
    '',
  ].join('\n');
}

export function collectComponents(repoRoot: string = REPO_ROOT): GeneratedFile[] {
  const ids = new Set<string>();
  for (const c of PLATFORM_COMPONENTS) {
    if (ids.has(c.id)) throw new Error(`组件 id 重复：${c.id}`);
    ids.add(c.id);
  }
  const files: GeneratedFile[] = PLATFORM_COMPONENTS.map((c) => ({ path: `platform/${c.id}.md`, content: componentPage(c, repoRoot) }));

  const platformRows = PLATFORM_COMPONENTS.map((c) => `| [${c.id}](/components/platform/${c.id}) | ${c.name} | ${c.stage} | ${c.summary} |`).join('\n');
  files.push({
    path: 'platform/index.md',
    content: [
      '---',
      `title: ${yamlString('平台插件')}`,
      `description: ${yamlString('身份、鉴权、租户、文件、配置、事件、审计等平台插件的目录。')}`,
      'part: components',
      'project: qian',
      'audience: [developer, operator]',
      `updated: ${repoFileDate(SELF, repoRoot)}`,
      '---',
      '',
      '# 平台插件',
      '',
      '多租户平台的能力都以组件交付：租户、身份与 SSO、鉴权、配置、事件与消息、调度与实时、权益等。规划中的组件清单来自 PRD §7 与组件模型规格 §7.7；带代码的组件同时可作为 NuGet 包独立使用（ADR-019）。',
      '',
      PLACEHOLDER_NOTE.replace('本页由收集脚本', '各组件页由收集脚本'),
      '',
      '| 组件 | 名称 | 阶段 | 用途 |',
      '|---|---|---|---|',
      platformRows,
      '',
    ].join('\n'),
  });

  const yueRows = YUE_PACKAGES.map((p) => `| \`${p.name}\` | ${p.stage} | ${p.summary} |`).join('\n');
  files.push({
    path: 'yue/index.md',
    content: [
      '---',
      `title: ${yamlString('yue 组件库')}`,
      `description: ${yamlString('yue 组件库各包的目录；组件页带活演示与 props 表。')}`,
      'part: components',
      'project: yue',
      'audience: [developer]',
      `updated: ${repoFileDate(SELF, repoRoot)}`,
      '---',
      '',
      '# yue 组件库',
      '',
      `yue 是 Qian 的前端组件库（[ADR-013](${designRoute('docs/adr/ADR-013-frontend-yue.md')})、[yue 专题规格](${designRoute('docs/prd/specs/yue.md')})）。S1-DOCS-002 起每个组件一页，带活演示与 props 表；下表是 S1 交付的包。`,
      '',
      '| 包 | 阶段 | 内容 |',
      '|---|---|---|',
      yueRows,
      '',
    ].join('\n'),
  });

  files.push({
    path: 'index.md',
    content: [
      '---',
      `title: ${yamlString('组件目录')}`,
      `description: ${yamlString('每个组件一页：怎么装、怎么配、暴露了什么；平台插件与 yue 组件用同一模板。')}`,
      'part: components',
      'project: qian',
      'audience: [developer, operator, isv]',
      `updated: ${repoFileDate(SELF, repoRoot)}`,
      '---',
      '',
      '# 组件目录',
      '',
      '「组件怎么用」只写在这一部分：每个组件一页，讲用途、安装（`qian add <id>`）、配置、表面、API、示例、模型体验与已知限制。编程模型与 API 原理在 [SDK](/sdk/)，命令与清单字段在 [CLI 与规格](/cli/)。',
      '',
      `- [平台插件](/components/platform/)：${PLATFORM_COMPONENTS.length} 个规划中的组件`,
      '- [yue 组件库](/components/yue/)：前端组件与外壳',
      '',
      '本部分随 Qian 包族的 SemVer 多版本发布；首个版本发布前只有开发版。',
      '',
    ].join('\n'),
  });

  files.push({ path: '_meta.json', content: `${JSON.stringify(['index', { type: 'dir', name: 'platform', label: '平台插件' }, { type: 'dir', name: 'yue', label: 'yue 组件库' }], null, 2)}\n` });
  files.push({ path: 'platform/_meta.json', content: `${JSON.stringify(['index', ...PLATFORM_COMPONENTS.map((c) => c.id)], null, 2)}\n` });
  files.push({ path: 'yue/_meta.json', content: `${JSON.stringify(['index'], null, 2)}\n` });
  return files;
}
