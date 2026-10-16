> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# ADR-007 配置中心 Apollo

> 本页由构建脚本镜像自仓库 [`docs/adr/ADR-007-config-center-apollo.md`](https://github.com/idadawn/qian/blob/main/docs/adr/ADR-007-config-center-apollo.md)，请修改源文件，不要改本页。

## 1 背景与问题（Context）

Apollo 是硬性要求。Kun 的 `Kun.Configuration` 只做 `AddKunOptions<T>` 绑定 + 校验，**无任何配置源**，全仓无 `apollo`；现网没有 Apollo、没有 MySQL、没有任何配置中心被应用消费；Nacos 3.0.1 standalone 已部署但只作 AI Registry PoC，且既有边界 ADR 把「动态非敏感配置」权威划给了 Nacos Config，iClaw 的 `IConfigProvider` 设计也定为 Nacos（基线 K §2.6；基线 P 附录 A-2）。

Apollo 的隐性成本：MySQL 是其唯一官方生产库（PostgreSQL 不受支持）而现网全 PG；官方 .NET 客户端 2.11.0 维护停滞、反射式 STJ 不适配 AOT / 裁剪、启动同步阻塞且无缓存时 30 秒后以空配置继续；Apollo 无租户概念、namespace 有上限、灰度只按实例；.NET 10 运行时的 `IOptionsMonitor` 热重载会重跑校验但不保留上一份有效值，`CurrentValue` 持续抛异常（基线 K §2.6、§7.1 第 4 条、附录 B-7）。版本：≥2.5.2 修复三个 CVE；3.0.0 于 2026-10-03 发布，生产等首个补丁（基线 K §6.3）。参考平台把 100 多个配置键扁平堆放、密钥与参数同表、文件路径与 OCR 地址写在后端配置文件（基线 S §6.2 第 9 条；基线 F §6 第 10 条）。

## 2 决策（Decision）

- **D7.1 Apollo 作为 L1 共享平台组件**，按现有「vendor chart + 离线渲染入 git」范式部署：configservice + adminservice（每 Apollo 环境一组，2 副本 + podAntiAffinity + PDB）+ portal（2 副本，OIDC + JDBC session）；`JAVA_OPTS=-XX:MaxRAMPercentage=70`；Portal 接 Authentik OIDC、不公网。
- **D7.2 版本**：首发 2.5.x（≥2.5.2）；3.0.x 待首个补丁后评估升级（Q8）。
- **D7.3 MySQL**：staging 先用集群内 operator，生产用托管 RDS（Q8）。
- **D7.4 不与 iClaw 共用一套 Apollo**（Q8）。
- **D7.5 .NET 客户端自研轻量 provider**（`qian.config`，路线 B）：按公开 HTTP 协议 `/configs`、`/notifications/v2` 长轮询（60 秒 hold，需大于 60 秒的空闲超时）、AccessKey HMAC 签名；集群内直连 Service 跳过 Meta Server；AOT 友好、源生成序列化；本地缓存文件；官方 SDK 只作 PoC。
- **D7.6 `Kun.Configuration` 增强**：运行期变更先校验，失败保留上一份有效值并告警；`ValidateOnStart` 分「关键」与「可选」配置；options JSON Schema 导出；失败降级矩阵 L0–L4。
- **D7.7 边界**：Apollo 只存平台 / 服务级**非敏感**配置与密钥引用（`secretRef`）；密钥走 K8s Secret / Infisical，由 `ISecretResolver` 在使用点解析；清单、事件、日志永不出现明文密钥。
- **D7.8 租户级配置不进 Apollo**，由 `qian.tenancy` 的租户配置服务承载（S1 基础 / S3 自助）；功能开关的租户灰度靠租户配置服务，Apollo Label 灰度只按实例。
- **D7.9 映射**：AppId = 一个微服务 / 后端插件；Environment 只建 UAT + PRO；Cluster 默认 `default`（仅区域或专属大租户用）；私有 namespace `application` 与 `qian.plugin.<id>`，公共 namespace `qian.common.*`；插件 `config.schema`（JSON Schema 2020-12）映射为 namespace 骨架与校验器，敏感字段标 `sensitive` 且只能来自 `secretRef`。
- **D7.10 配置源顺序**：appsettings → Apollo（公共 → 插件 → application）→ K8s Secret 文件（KeyPerFile）→ 环境变量 → 命令行；Kustomize 只注入引导项（`Apollo__AppId / Env / Cluster / ConfigServer__0 / Label / CacheDir`、Secret 文件）。
- **D7.11 授权与开关分开存**：授权（Entitled，是否买了）在租户 / 订阅库；特性开关（Enabled，是否灰度开）在 Apollo（OpenFeature 式 targetingKey = tenantId）。
- **D7.12 修订既有边界 ADR**：Nacos 收窄为 AI 资产 / MCP 注册，不再承担配置职责；iClaw 若坚持 Nacos 则「各用各的、互不越界」，或为其 `IConfigProvider` 端口提供 Apollo 实现。
- **D7.13 治理**：webhook / OpenAPI 扫描拦截疑似密钥键名与值；Apollo 发布事件进 `qian.audit`；实例拉取审计作为「变更是否生效」的回读信号；基线定期导出进 git。

## 3 理由（Rationale）

- Apollo 为硬性要求且已被裁定为 Qian 侧配置权威（基线 K 附录 B-8）；分叉成本在于修订边界 ADR 与新增 MySQL 运维面，但比双配置中心长期并存便宜。
- 自研 provider 的原因是官方客户端停滞且无法通过 AOT / 裁剪门禁、启动阻塞后以空配置继续运行是生产不可接受的行为（基线 K §2.6）。
- 租户级配置不进 Apollo 是 Apollo 自身的能力边界决定的（无租户概念、namespace 上限、灰度粒度），而不是偏好（基线 K §6.3 第 4 条）。

## 4 备选方案与放弃原因（Alternatives）

- **Nacos Config 作为 Qian 配置中心**：违背硬性要求；但若团队最终接受 Nacos 也能满足，这是一个显式分叉点（基线 K §7.1 第 4 条）。放弃。
- **官方 .NET SDK**：维护停滞、反射序列化、启动行为不安全。只作 PoC。
- **只用 K8s ConfigMap + GitOps**：无运行期变更推送、无灰度、无审计回读。放弃。
- **Apollo 承载租户级配置**：namespace 上限与无租户概念使其不可扩展。放弃。

## 5 后果与代价（Consequences）

- 正面：平台 / 服务级配置有统一的发布、审计与回读；插件配置 Schema 化并由清单派生 namespace；密钥与配置彻底分离。
- 负面：新增 MySQL 运维面（现网无平台级 MySQL 经验）与 Apollo 自身的 JVM 资源；与 GitOps 形成双真相，需要基线导出纪律；自研 provider 要自己跟进 Apollo 协议变化。
- 配套：L1 新增 `apollo/` 目录（ns `apollo`）；`qian.tenancy` 租户配置服务；S2 控制台渲染插件配置；S3 灰度 / 审核 / 回滚编排。

过渡态（2026-10-06）：现网无 Apollo、无 MySQL，边界 ADR 仍指向 Nacos Config；S1 第一个迭代完成 L1 部署与 ADR 修订前，Qian 服务以 appsettings + K8s Secret 启动。

## 6 对各阶段的影响

- **S1**：Apollo L1 部署 + MySQL；自研 provider；热更新语义；`secretRef`；配置 Schema 声明；租户配置服务基础。
- **S2**：控制台渲染插件配置；配置变更审批。
- **S3**：配置灰度 / 审核 / 回滚编排；租户自助配置。
- **S4**：配置 Schema 作为 Agent 可读元数据的一部分，写入走审批。

## 7 待确认事项

- 3.0.x 升级时点与 2.5.x 的 CVE 跟进节奏。
- 生产托管 RDS 的供应商与网络打通方式。
- Infisical 运行时注入（Operator / ESO）未接通前，`secretRef` 的运行时权威只能是 K8s Secret。
- Apollo 实例级灰度与租户级开关之间的联动规则需在 S2 前验证。

## 8 参考

- 相关 ADR：ADR-001、ADR-002、ADR-014、ADR-016、ADR-017。
- PRD：`PRD §07`、`PRD §12`、`PRD §13`。
- 基线：K §2.6、§6.2 配置 / 密钥行、§6.3、§7.1 第 4 条、附录 B-7/B-8；P §5 配置绑定行、附录 A-2；S §6.2 第 9 条、§11 第 7 条。
