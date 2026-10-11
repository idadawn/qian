---
title: 部署：沿用现有 K3s / ArgoCD / Kustomize / Harbor 约定；Qian 独立命名空间 + 专属 PG/Redis；不以 per-tenant namespace 为默认
status: Accepted
date: 2026-10-06
decision_id: D17
supersedes: none
---

# ADR-017 部署

## 1 背景与问题（Context）

Kun 仓库没有 Dockerfile、compose、Helm、manifest、健康端点映射或镜像构建脚本；K8s 只出现在文档假设（基线 K §2.5）。团队另有成熟的部署约定（iclaw-deploy）：单集群 K3s v1.35 三节点全 control-plane（embedded etcd），只跨两台物理宿主；几乎所有有状态与共享服务 `nodeSelector` 钉首节点（同时是唯一 GPU 节点与 CI runner）；唯一 StorageClass `local-path`；同集群按 ns 分环境（prod / staging / mock）；Argo CD app-of-apps（根 Application `selfHeal:true`，业务 Application `automated{prune:false, selfHeal:false}`）；Kustomize base / overlay；L0 / L1 / L2 三层 + 三个互斥 Argo Application + 静态边界门；不可变发布事件（40 位 SHA、CalVer）→ 验签 → 镜像推 Harbor → DB 迁移 Job 先行 → `kustomize edit set image` 按 digest 写回 → Argo 收敛 → smoke；Syft / Grype / Trivy / Cosign 供应链证据；「vendor chart + 离线渲染入 git」范式；秘密只存模板 / 键名 / secretRef（基线 K §4.2、§6.1）。

治理缺口：无 ResourceQuota / LimitRange / HPA；NetworkPolicy 业务 ns 为零；PDB 只有 3 个；多处 `:latest`；无 Alertmanager；Infisical 已部署但运行时注入未接通；共享 Redis（`allkeys-lru`）被 L1 的 Authentik / Infisical 跨 ns 复用；K3s 上无 RabbitMQ；prod 与 staging 共用同一 OIDC client（基线 K §6.1、§4.2、§7.1 第 5 条）。

## 2 决策（Decision）

- **D17.1 沿用 iclaw-deploy 全部约定**：L0 / L1 / L2 三层；Argo CD app-of-apps；Kustomize base / overlay；不可变发布事件；按 digest 写回；迁移 Job 先行；供应链证据（SBOM / 扫描 / 签名）；环境差异只放 overlay 与机器本地 Secret，禁止代码内按环境分叉。
- **D17.2 Qian 新建 `qian-<env>` 命名空间与专属 PostgreSQL 16 / Redis 7**；不把共享平台挂到应用 Redis 上，不重复 L1 反向依赖 L2 的反例。
- **D17.3 L1 新增**：Apollo + MySQL（ADR-007）、RabbitMQ（ADR-008）；Authentik 升级并仅服务运维侧；Infisical 运行时注入补 Operator / ESO 后才算权威。
- **D17.4 每个服务 `/healthz`（liveness）与 `/readyz`（readiness，含 PG / Redis / RabbitMQ / Apollo 依赖检查）**；优雅停机 `terminationGracePeriodSeconds` ≥ 优雅停机超时 + 10s；Apollo / Higress / Nacos / Infisical 控制台不公网。
- **D17.5 治理补齐**：业务命名空间 default-deny NetworkPolicy（出口规则由插件清单 `permissions.egress` 生成）+ ResourceQuota / LimitRange；补 Alertmanager；禁止 `:latest`；K3s 打开 `--secrets-encryption`。
- **D17.6 多租户拓扑三档**：默认共享 ns / 共享 Pod，数据按 `tenant_id` / RLS 隔离；高隔离租户独立 ns + 独立 PG / Redis，由 ApplicationSet 生成；私有化客户整套 `env-<customer>/` overlay 复制。**不以 per-tenant namespace 为默认**；每档都要 ResourceQuota / LimitRange + default-deny。
- **D17.7 生产集群**（Q16）：与 iClaw 共用同一 K3s（同 Argo / Authentik / Higress）但使用**独立节点池**；达到一定租户数再拆独立集群；加节点时按物理宿主计故障域。
- **D17.8 第二类制品「插件包（OCI artifact）」推 Harbor**，清单只允许 digest 引用；代码制品 = 清单 + 文件摘要 + SBOM，cosign 签名与证明；插件制品走同一发布链路。
- **D17.9 发布节奏**：内核随 CalVer 发布；插件独立灰度（Apollo Label 灰度只按实例，租户灰度靠 `qian.tenancy` 开关）；`prune:false` 起步。
- **D17.10 配置与密钥注入**：Kustomize 只注入引导项（`Apollo__*`、Secret 文件）；配置源顺序见 ADR-007；仓库只存模板 / 键名 / secretRef；bootstrap 脚本「只建缺失、不覆盖、不打印」。
- **D17.11 身份提供方部署**：客户 CIAM = `qian.identity`；运维侧 = Authentik（升级到当前版本，注意跨 Redis 移除等破坏性变更）；per-env 至少独立 OIDC client；Apollo Portal / Headlamp / Argo 走 Authentik blueprint。
- **D17.12 镜像**：业务服务 JIT 镜像；网关 / gRPC 边缘可 AOT distroless；`qian plugin pack` 从清单产出 Dockerfile / chart / NetworkPolicy 片段；`service` 型插件由生命周期控制器生成 Kustomize / Helm 片段写入 GitOps 仓库、Argo 收敛，健康 = K8s 状态 + 插件 `/health` 双条件回写注册表。
- **D17.13 租户接线**：每个租户 / 应用在共享网关、IdP、Apollo 上的配置单独一个 Argo Application（沿用 `*-integrations` 模式）；S3 接线 Application 生成器（开通 = 写 Git → Argo 收敛）。
- **D17.14 入口**：两个 IngressClass（`traefik` 业务 / `higress` AI）各有 MetalLB VIP；公网 nginx 终结 TLS；Traefik `trustedIPs` 必配；cert-manager 按需引入。

## 3 理由（Rationale）

- 部署约定已在 iClaw 生产运行，复用成本最低、风险最小（基线 K §4.2）。
- 不以 per-tenant namespace 为默认：三节点集群资源与故障域不支持数千租户各一套命名空间；共享 + RLS 已是数据层默认（ADR-004）；高隔离与私有化作为显式档位满足少数客户。
- 独立节点池是在「现网故障域是整台首节点」与「拆集群的运维成本」之间的折中；按租户数设拆分阈值让决策可延后。
- 治理缺口必须在多租户上线前补齐，否则一个租户的资源失控会影响整个集群（基线 K §7.1 第 5 条）。

## 4 备选方案与放弃原因（Alternatives）

- **独立生产集群**：故障域最清晰，但 Argo / Authentik / Higress / Harbor 都要复制一套；达到租户数阈值后再做。暂不采用。
- **per-tenant namespace 默认**：资源与运维不可承受。放弃。
- **Helm 全面替代 Kustomize**：与现网范式冲突；Helm 只用于 vendor chart 离线渲染。放弃。
- **Dapr / Service Mesh**：见 ADR-002。放弃。

## 5 后果与代价（Consequences）

- 正面：Qian 第一天就有 GitOps、供应链证据与健康约定；插件包与镜像走同一条发布链。
- 负面：共用集群意味着 iClaw 的故障域部分与 Qian 重叠；L1 新增 Apollo + MySQL + RabbitMQ 增加运维对象；`local-path` 唯一存储类限制有状态组件的 HA。
- 配套：ResourceQuota / NetworkPolicy 模板；ApplicationSet 模板；Alertmanager 路由；拆集群阈值与迁移预案。

过渡态（2026-10-06）：业务 ns 无 NetworkPolicy / ResourceQuota，无 Alertmanager，K3s 上无 RabbitMQ / Apollo / MySQL，Infisical 注入未接通，prod / staging 共用 OIDC client。D17.3–D17.5 是 S1 第一批基础设施任务，完成前 Qian 不进入多租户生产。

## 6 对各阶段的影响

- **S1**：镜像与 chart、健康端点、`qian-<env>` ns + 专属 PG / Redis、RabbitMQ / Apollo K8s 化、default-deny + ResourceQuota、插件包 OCI 制品、Alertmanager 可选。
- **S2**：逐租户迁移 Job 的批次编排；多副本迁移协调。
- **S3**：接线 Application 生成器、ApplicationSet 高隔离档、专属部署、私有化 overlay 复制清单。
- **S4**：CubeSandbox 已在集群（DaemonSet），Qian 只做对接；GPU 节点池规划。

## 7 待确认事项

- 拆独立集群的租户数阈值与节点池划分。
- cert-manager 引入时点。
- Infisical Operator / ESO 的落地时点。
- GPU 节点归属与是否进入 Qian 节点池（见 ADR-015）。

## 8 参考

- 相关 ADR：ADR-002、ADR-004、ADR-007、ADR-008、ADR-010、ADR-014、ADR-015。
- PRD：`PRD §07`、`PRD §09`、`PRD §12`、`PRD §14`。
- 基线：K §2.5、§4.2、§6.1–§6.3、§7.1 第 5 条、§7.2 第 7 条；P §4.6、§10.1 第 8 条。

## 9 基线与真实平台联调分阶段验收（2026-10-11）

产品负责人已通过 [S1-DEC-016](../../pm/tasks/S1-DEC-016.md) 确认分阶段验收，消除 DEP-001 要求 L1/应用先就绪、DEP-002/003 又依赖 DEP-001 的循环。

- 基线阶段（S1-DEP-001）：在真实集群验证两环境独立命名空间、专属 PG/Redis、默认拒绝网络策略、配额/限制、生产独立节点池与入口/加密基线。staging 用明确标识的诊断工作负载验证拒绝路径与已授权的 PG/Redis 协议访问；其结果只证明基础设施与策略，不标作真实 qian-platform 联调。
- 应用阶段（S1-DEP-005 发布验收）：待 DEP-002 的专属 RabbitMQ/Apollo 和 DEP-003 的真实平台镜像就绪，补充精确网络放行，从真实 qian-platform 验证 PG、Redis、RabbitMQ、Apollo 四类依赖的协议与认证，并关联应用就绪状态。该项是发布必过门，不能以探针、同名标签 Pod、共享服务或仅 TCP 连通代替。

DEP-002/003 的基线前置保持，DEP-005 显式依赖 DEP-002，并经 DEP-004 依赖 DEP-003。上述裁决只调整验收顺序，不豁免真实集群检查、生产节点池、实际 HTTPS issuer、secrets-encryption、制品证据或发布准入；静态清单和本机容器通过不能将 DEP-001 标为完成。
