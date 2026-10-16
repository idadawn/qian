> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# ADR-002 插件模型

> 本页由构建脚本镜像自仓库 [`docs/adr/ADR-002-plugin-model.md`](https://github.com/idadawn/qian/blob/main/docs/adr/ADR-002-plugin-model.md)，请修改源文件，不要改本页。

> **2026-10-06 部分被 ADR-020 取代。** D2.1 的形态轴（`kind` 七值）退为 `runtime.form` 的实现细节，顶层分类改为「表面（surface）」；D2.9 的租户级生命周期改为租户 scope patch（授权状态机只保留「买没买」）；D2.10 的「预装全部官方 `module` + 按租户开关」改为 `platform` profile 默认组合包 + scope patch。其余条目保留并映射到新字段：D2.4 信任等级 → `policy.tier` / `policy.sandbox`；D2.6 清单从代码派生 → 代码部分字段派生后合并进唯一的 `plugin.json`；D2.7 digest 引用 → 仅代码部分的 OCI 制品；D2.8 能力条目的 Agent 字段、D2.11 Wasm 阶段、D2.12 明确不做不变。修订清单见 ADR-020 §6；被取代条目在下文行首标注、原文保留。

## 1 背景与问题（Context）

Qian 以「插件思维」组织一切能力，但 Kun 决定了插件的物理形态：Kun 是「每个能力一个 NuGet、一个 `AddKunXxx()` 入口、显式注册、严格 AOT、禁动态加载」的框架，对 AOT 候选模块强制 `EnableDynamicLoading=false`，禁用 `AssemblyLoadContext` 类 API（基线 P §0 第 2 条、§2.3）。因此进程内插件只能是编译期并入的模块，任何运行时新增代码的能力必须进程外；.NET 官方立场是「不可信代码无法安全载入受信进程」（基线 P §2.4）。

参考平台没有插件机制：内核唯一的外部程序集入口是启动期一次性加载且应用未使用；内核 11 万行加公共层 3.4 万行且公共层反向依赖业务实体；鉴权分散在控制器且可被客户端参数绕过（基线 P §0 第 8 条、§1.3）。多个开源低代码 / AI 平台的许可证分别禁止用其代码向公众提供低代码 PaaS、禁止未授权多租户运营或为 AGPL，只能借鉴设计不能引入代码（基线 P §2.4；基线 K §7.1 第 8 条）。

基线 P 对比了七种候选形态（进程内模块、独立微服务、元数据包、外部 SaaS、Wasm 函数、前端远程插件、平台托管容器），并指出 d6 的六种 kind 与 d3 的五种包类型描述的是不同维度（基线 P §2.1、§2.2 裁决 A-11）；本 ADR 在六种之上把复合包 `bundle` 也定为顶层 `kind`，共七值。

## 2 决策（Decision）

- 【已被 ADR-020 取代】**D2.1 三个正交轴**：`kind`（顶层七值：`module` 进程内编译期并入 / `service` 独立 Pod / `package` 元数据 / `function` Wasm / `connector` / `ui` / `bundle` 复合包；`bundle` 的 `components` 下各组件的 `kind` 取前六值）× `role`（`provider` / `capability` / `extension` / `app` / `theme` / `agent`）× `distribution`（`template` 拷贝即拥有 / `managed` 托管可升级，仅对 package / bundle 有意义）。
- **D2.2 应用 = bundle**：一个「应用」是 `kind: bundle` 的复合插件包，`components` 下可同时含 `package + ui + function (+ service)`，附种子数据、迁移脚本、依赖清单与 id 重映射表（细节见 ADR-014）。
- **D2.3 系统能力也是插件**：租户、身份、鉴权、存储、文件、配置、网关、模型网关等平台自带技术组件全部以 `tier: system`、`alwaysEnabled: true` 按同一清单交付，同一生命周期、同一 TCK；任一系统插件必须能独立出修订、逐租户迁移、回切。
- **D2.4 信任等级决定形态**：`system / official / partner / private / community` 五级；第三方代码永不进共享进程；`private / community` 只允许 `package / function / connector / 受限 ui`；`partner` 可加 `service`（独立工作负载身份、独立 PG schema 与数据库角色、事件主题 ACL、出口 NetworkPolicy）。
- **D2.5 S3 不开放第三方 `service`（任意代码容器）上架**，只开放 `package / function / connector / 受限 ui`（Q9 裁决）。
- **D2.6 清单先于代码、从代码派生、静态可索引**：`qian-plugin.yaml`（JSON Schema 2020-12 校验）由源生成器与 CLI 从代码派生，不手写；宿主不执行插件代码即可据清单生成路由、权限点、菜单、Apollo namespace、OpenAPI 聚合与 MCP 工具描述；CI 比对清单与代码，漂移即失败。
- **D2.7 清单关键约束**：`id` 为反向域名、发布后不可改；插件 SemVer 与 Kun CalVer 独立；`engines.qian/kun/yue` 声明兼容；OCI 制品只接受 `@sha256:` 引用；签名不在清单内（代码制品用 OCI + cosign，元数据包用文件摘要清单 + 分离签名）；未签名或签名不符直接安装失败。
- **D2.8 `capabilities` 条目是 Agent 可发现的最小单元**：带 `sideEffect read|write|external|destructive`、`policyHint allow|confirm|deny`、`requiredScopes`、`schemaSha256`（schema 变即旧授权失效）；`inputSchema` 禁止出现租户字段。两个方向的权限：`provides.permissions`（本插件定义的权限点）与反向权限 `permissions.platform/data/egress/secrets/ui`（租户管理员开通时逐项批准，升级新增即重批）。
- 【已被 ADR-020 取代】**D2.9 两层生命周期**：平台级 `Draft → Submitted → Verified → Published → Installed(revision n) → Active → Deprecated → Retired`，保留 N 个旧修订可回切；租户级 `Available → Entitled → Provisioning → Enabled ⇄ Disabled → Upgrading / Uninstalled → Purged`；租户级钩子逐租户、幂等、可重试、结果入库；`Entitled`（授权库）与 `Enabled`（Apollo 开关）分开存；「未启用」在网关路由、SDK 入口、前端外壳三处同时拦截。
- 【已被 ADR-020 取代】**D2.10 S1 接受「预装全部官方 `module` + 按租户开关」**；运行时装载只对 `package / service` 成立。
- **D2.11 Wasm `function` 运行时为 S2 可选（P2，不计入 S2 核心交付）**：服务端公式与规则先用 JSON AST 表达式解释器；Wasm 在 ISV 函数插件（S3）前落地；ABI（Extism 或 WIT 组件）在 S2 中期拍板。
- **D2.12 明确不做**：运行时热插 DLL；参考平台式程序集目录加载；进程共享的服务端 JS 引擎；Dapr sidecar 作为默认依赖；引入 NocoBase / Dify / Grafana / n8n 代码。

## 3 理由（Rationale）

- 三轴分解把「组件形态」「功能角色」「分发与可定制性」拆开，避免 kind 与包类型的概念混叠（基线 P §2.2）。
- 清单静态可索引是 Grafana `plugin.json`、VS Code `contributes` 验证过的做法；Kun 没有源生成器、注册靠手写，Qian SDK 必须补（基线 P §1.1 P3/P4）。
- 按 digest 引用源于 Crossplane 按 tag 校验的 TOCTOU 漏洞教训；强制验签源于 Dify 自托管默认仅警告的反例（基线 P §3.1、§4.5）。
- 预装 + 开关与 Kun 编译期组合兼容，是 S1 唯一不依赖 JIT 热加载的可行路径（基线 P §6 补充说明）。

## 4 备选方案与放弃原因（Alternatives）

- **热插 DLL / 程序集目录加载**：Native AOT 不支持 `Assembly.LoadFile`；无法卸载、无隔离。放弃。
- **Dapr sidecar**：三节点 K3s 每 Pod 一个 sidecar 代价高，且与 Kun EventBus/Outbox、S2S、YARP 大面积重叠；只借鉴其组件 YAML 声明方式。放弃。
- **进程共享服务端 JS 引擎**：参考平台全局单例 V8 无锁无超时无沙箱；用 Wasm `function` 替代。放弃。
- **S3 开放第三方任意代码容器**：审核与运维投入量级陡增，三节点集群每个 `service` 一个 Deployment 很快吃紧；参照「禁第三方代码但允许纯数据模块」的做法先只开放声明式形态。放弃（可在 S3 后期重议）。

## 5 后果与代价（Consequences）

- 正面：一份清单派生十类产物，新增能力后目录、权限、MCP 三处自动出现；信任等级与形态绑定使安全边界可检验。
- 负面：官方 `module` 升级等于重建宿主镜像；元数据 `package` 表达力受 schema 限制、需要强大解释器；Wasm 宿主在 .NET 侧缺组件模型支持（wasmtime-dotnet 不支持组件模型），选 WIT 要多维护一个 Rust 宿主。
- 配套：`qian.plugin-sdk` 必须自带源生成器、CLI（`new/dev/test/pack/sign/publish`）与 TCK（清单校验、契约兼容、钩子幂等、跨租户越权探测）；注册表与生命周期控制器为 S1 必做。

## 6 对各阶段的影响

- **S1**：清单 v1 Schema + 校验器 + 源生成器；注册表与两层状态机；强制验签、digest 引用、修订回切；bundle 格式 v0；外壳插槽。
- **S2**：`package` 元数据插件、`distribution` 字段、逐租户批量迁移、升级同意规则；`function` 可选。
- **S3**：`partner / private / community` 对外生效；审核流水线、工具描述哈希钉死；`connector` 运行时。
- **S4**：`role: agent` bundle、`agent.mcp.expose` 实际派生；Agent 生成应用 = 生成 `package`，代码只落 `function`。

## 7 待确认事项

- 是否需要一个 JIT 的官方插件宿主以支持「不重建镜像上新官方插件」（基线 P §10.2 第 2 条；D1.5 已定业务服务全 JIT，但宿主拆分未定）。
- `function` ABI：Extism（.NET 进程内可跑）还是 WIT 组件（标准化但需独立 Rust 宿主），S2 中期拍板。
- MF 共享依赖漂移与 yue 主版本并存期策略（见 ADR-013）。

## 8 参考

- 相关 ADR：ADR-001、ADR-003、ADR-013、ADR-014、ADR-016。
- PRD：`PRD §02`、`PRD §03`（核心概念模型）、`PRD §07`、`PRD §12`、`PRD §13`（权限与安全）。
- 基线：P §0、§1.1、§1.3、§2.1–§2.4、§3、§4.1–§4.7、§10.1、附录 A-8/A-9/A-11；K §3.1 第 7–9 条、§7.1 第 6/8 条。
