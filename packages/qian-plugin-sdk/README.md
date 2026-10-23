# Qian 前端插件 SDK

逻辑名 `@qian/plugin-sdk`，私有 GitHub Packages 发布名 `@idadawn/qian-plugin-sdk`。目录模型与运行时语义见 [组件规格](../../docs/prd/specs/component-model.md)，任务范围见 [S1-YUE-001](../../pm/tasks/S1-YUE-001.md)。本包提供外壳 Definition 与运行时适配；yue 保持可独立使用，不依赖 Qian。

## 安装与发布

消费项目配置 `@idadawn:registry=https://npm.pkg.github.com`，认证行只引用环境变量 `//npm.pkg.github.com/:_authToken=${NODE_AUTH_TOKEN}`，不能提交令牌值。逻辑导入名通过 npm alias 保留：

```bash
npm install '@qian/plugin-sdk@npm:@idadawn/qian-plugin-sdk@0.1.0'
```

依赖为实际发布的 `@idadawn/qian-runtime`、`@idadawn/yue-sdk`（传递 contracts 与 tokens）、官方 MF2 runtime 与 SemVer；Ajv 只在构建期使用（见下文「校验不在运行期生成代码」）。`pnpm pack` 会把 workspace 依赖写成固定版本范围，包内只交付 ESM、声明与文档；本地 pack 不代表私有源发布成功。正式发布与外部干净消费者验收由 `plugin-sdk-release` 工作流执行。

### 私有包初始化与正式发布

`plugin-sdk-release` 仅接受 main、`idadawn/qian` 与清单中的固定 SDK 版本；所有远端写入使用本次 Actions 的 `GITHUB_TOKEN`，不使用本机 PAT 回退。固定依赖组只有 `yue-tokens / yue-contracts / yue-sdk / qian-plugin-sdk` 四个 `@idadawn` 包；既有 `qian-runtime` 只参与消费者验证，不重复发布。

默认 `operation=release` 保留构建、测试、打包、仓库外归档消费、同版本摘要防覆盖和真实 registry 消费。**任何正式归档上传前，先 GET 核对完整四包均已存在且 visibility=private**；缺包、public 或 internal 会使整组失败，正式上传次数为零。每次上传前后再检查可见性，不能把 npm 的 `--access=restricted` 输出当作 GitHub 私有状态的证明。

首次缺包时显式选择 `operation=bootstrap-private`，`version` 仍填写正式 SDK 目标版本。这个模式不安装工作区依赖、不构建或读取 `dist`；它为不存在的固定名称生成仅含 `package.json / README.md` 的 `0.0.0-private.0` 空版本，以 `private-bootstrap` 标签发布并关联本仓库。上传前检查真实归档只有两份文件，没有实现、依赖或安装脚本。已有名称不发布或修改。输出包含 `bootstrapRunId`、实际可见性和 `implementationPublished=false`；若有非 private 包，工作流以 `needs-admin` 失败结束，须管理员在包设置中调整为 private 并确认 Actions 访问权限。这是可见性初始化，不能当作正式 SDK 交付；空初始化版本可能先处于 public，但正式实现尚未上传。

管理员配置完成后再次选择默认 `release`，仍发布完整 `0.1.0`（或输入的固定正式版本），并通过私有源真实安装验收。可选输入 `bootstrap_run_id` 必须填写**原初始化运行 ID**：仅在本次 registry 消费成功后，清理才会执行。清理再次确认四份正式版本和摘要，读取空版本归档，核对本仓库、本轮 runId、包名、目标版本、精确空版本及完整两文件内容；只调用该版本 ID 的删除接口。正式版本、其他运行的空版本、无标记或带未知文件的版本均保留；留空不执行删除。

`node --test packages/qian-plugin-sdk/scripts/private-publication.test.mjs` 验证整组零上传拒绝、初始化归档无实现、既有包不变及清理防误删，不向真实 registry 发布或删除任何内容。

## 宿主与远程

宿主创建 `ShellHost`，提供身份上下文、挂载、权益、权限、同一个 `PlatformClient`、导航/通知处理器以及必需的 `audit` 适配器。`start()` 挂载真实 `qian.shell` 根条目，`registerRemote()` 只登记可信清单，不发网络请求。`catalog(slot)` 由静态贡献生成导航；`loadSlot(slot, id)` 仅为当前页面需要的贡献调用 MF2 `registerRemotes / loadRemote`；`preloadSlot` 复用相同授权与版本检查。宿主可消费服务端已裁剪目录，但每次调用仍校验当前权限与 scope。

```ts
import { ShellHost } from '@qian/plugin-sdk';
import { createFederationTransport } from '@qian/plugin-sdk/federation';

// shared 工厂由宿主提供实际 React、yue 和 SDK 实例；不创建第二套 React。
const federation = createFederationTransport('example_shell', shared);
const host = new ShellHost({ ...trustedHostAdapters, federation });
await host.start();
host.registerRemote(trustedRemoteRegistration);
const menu = host.catalog('qian.shell.nav/v1');
await host.loadSlot('qian.shell.nav/v1', menu[0]?.descriptor.id);
```

官方可运行接线见 [qian.shell](../../apps/qian-shell/README.md)。`engines.yue` 在任何远程请求之前做 SemVer 检查；不满足时写结构化控制台事件，等待审计成功后抛 `ShellError`（`INPUT_INVALID`、`/engines/yue`）。审计失败向调用方传播，不能悄悄降级为只有控制台。错误细节沿冻结 ErrorDetail，错误目录由 `contracts/v0/error-codes.json` 派生并检查新鲜度。

远程 `mf-manifest.json`、`remoteEntry.js` 与 `@mf-types.zip / .d.ts` 由官方 Vite 插件生成。宿主采用 MF2 runtime 显式共享工厂；远程的 React、yue、SDK 全部 `import: false`，只能消费宿主 singleton。宿主完整根包按需加载，首次布局用现有 yue 的子路径；没有引入第二套 UI 引擎或裁剪后回落双份共享包。

## 插槽、作用域与桥接

`slotContracts` 导出六份带独立 `$id` 与 `/v1` 的输入 JSON Schema；`SlotOutputs` 定义各插槽输出，`listSlot()` 提供对应静态类型。`ui/manifest.json` 的 `pages[].nav` 转为导航贡献，其他表面通过 `contributions` 指定 `slot / id / title / description / permission / expose`；客户端动作另带 `parameters`。无效 Schema、未知字段、未声明 expose 与重复贡献拒绝登记。

**校验不在运行期生成代码（S1-YUE-012）**：外壳的 meta CSP 只放行同源脚本与按哈希列出的内联脚本，所以 SDK 的启动路径不得用 `new Function` / `eval`。UI 清单与插槽贡献的校验器由 `scripts/ui-schema.mjs` 在构建期用 Ajv standalone 预编译成 `src/ui-validators.generated.ts`（`--check` 比对新鲜度）；客户端动作的 `parameters` 在登记时由 `compileParameters` 解释执行一个封闭的 JSON Schema 子集（`type`、`enum`、`const`、`properties`、`required`、`additionalProperties`、对象与数组的个数约束、`items`、`uniqueItems`、字符串长度（按码点）与 `pattern`、数值范围与 `multipleOf`，注释类关键字忽略），子集之外的关键字拒绝登记（`INPUT_INVALID`、`/parameters`），嵌套深度上限 32。`test/parameters.test.ts` 另有静态检查，确认这些模块里没有 Ajv 运行时导入与动态代码生成。

每个加载的 expose 在真实 `PluginLoader` 中有自己的 Apply。贡献用 `ctx.provide(key, name, value)` 注册；相同槽位与名称的租户项遮蔽全局项。目录、读取、执行共用挂载/权益/权限过滤。`unloadRemote` 先撤销桥接访问，再逆序等待效果与注册释放；全局项随之重新可见。MF 已执行的 JavaScript 可能仍在浏览器模块缓存内，卸载承诺为逻辑贡献与生命周期撤销，不声称能清除 JavaScript 引擎代码缓存。

`window.__qian.bridge` 只公开调用入口，不公开注册身份的入口。宿主通过对象身份的 WeakMap 绑定真实远程 ID 与 scope，再将 `RemoteBridge` 交给对应远程。其中只读 `bridge.caller` 是受信任加载时生成的原对象句柄：远程可以调用 `window.__qian.bridge.getContext(bridge.caller)`，也可直接使用闭包形式的 `bridge.getContext()`；两条路径共享授权与撤销检查。自行填写同名 ID/scope、展开或 JSON 复制句柄都无效。每次调用重新检查注册仍有效、当前租户、挂载与权益，写操作额外验证能力声明与权限。卸载后保留的原句柄也失效，不提供公开 `open` 或 Dispatcher。

- `getContext()` 返回克隆且深冻结的 tenant、membership、app、locale、theme，不含令牌。
- `navigate()` 只接受清单白名单路由；导航资源引用沿现有 `Qian.Capabilities` 格式 `qian:<tenant>:<plugin>:page:<百分号编码路由>`，校验租户和插件后解析为同一路由。
- `notify()` 校验声明、等级和消息长度，消息作为文本处理。
- `invoke()` 复用 yue `PlatformClient`、请求拦截器和刷新队列；写类默认创建 UUID 幂等键并同时写入请求正文与 Idempotency-Key 头；也可从正文或选项传入同一键重试，两个来源冲突时拒绝请求。切换租户后的旧结果被拒绝，首次异步取令牌和刷新期间均不得改写旧租户请求。
- `registerClientAction()` 只能登记已声明的动作、参数 Schema 与权限，运行前再校验参数和权限；`exportClientActions()` 导出名称、描述、Schema、权限 JSON，卸载撤销旧执行引用。

## 模型体验

六类插槽与客户端动作的 JSON 目录可静态索引。过滤后的动作与不存在的动作统一 `NOT_FOUND`；Agent 不因此获得额外权限，也不从前端读取服务端工具或令牌。公开能力执行仍由服务端做最终鉴权、Schema 与幂等检查。

## 验证与边界

运行 `pnpm --filter @idadawn/qian-plugin-sdk test` 验证真实运行时遮蔽/卸载、深冻结、伪造身份、作用域与权限撤销、版本审计、网络等待时切租户、动作 Schema 和幂等请求；运行 `pnpm --filter qian-shell e2e` 验证真实 MF 制品、同实例 React/SDK/组件、预加载与类型归档、租户未挂载零请求和实际 PostgreSQL 审计链。

MF2 只加载可信官方 UI，不能隔离同一页面中的恶意 JavaScript。S3 的 Worker/iframe 沙箱独立推进。真实认证与会话刷新已由 YUE-012 接进外壳（`apps/qian-shell`），权益服务接线归 ENT-002，静态制品的镜像与部署归 DEP-008；这里交付明确的宿主注入端口、可部署静态目录及真实审计参考适配验收。
