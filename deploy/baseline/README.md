# S1-DEP-001 基线交付与现场状态

部署资产唯一源为部署仓 [PR #88](https://github.com/idadawn/iclaw-deploy/pull/88)：`2-qian/baseline`、`env-ds/qian-staging`、`env-ds/qian-prod`。Qian 仓库只记录任务与回链，不再复制一份生效清单。

## 已交付与已验证

- 两环境独立 namespace、PostgreSQL 16 / Redis 7 与独立 PVC；镜像按官方 registry 查询的多架构 digest 固定。
- default-deny ingress/egress；仅明确的应用/迁移 Pod 可连同环境 PG/Redis 与集群 DNS。RabbitMQ/Apollo 尚未放行，须专属实例与实际标签就绪后加入。
- ResourceQuota、LimitRange、PDB、非 root、只读根、资源限制与认证探针；生产数据服务强制指定独立节点池，不擅自复用现有节点。
- Secret 引导脚本只建缺失、不覆盖、不打印；清单里没有秘密值。PG 应用角色不能绕过 RLS，租户表仍须由各服务迁移启用并强制 RLS。
- 两个 overlay 可渲染，静态门及隔离/节点池/浮动镜像反例通过；本机固定镜像实际启动、PG 应用角色与双租户 RLS、Redis 哈希 ACL/匿名拒绝/认证探针通过。测试容器和临时凭据已清理。

## 现场阻塞

2026-10-11 04:34–04:42（Asia/Shanghai），通过 qd 做只读检查，Kubernetes API 返回 `ServiceUnavailable` / `apiserver not ready`；K3s 服务处于 `activating/start`，两次观察的累计重启数为 550 / 551。另外两个控制面在本次有限等待内未返回状态，不能据此推断节点宕机。加密状态检查未获得有效结果。

没有执行集群/节点变更，没有创建命名空间或现场测试 Pod；两份基线 overlay 尚未加入 Argo Application。Traefik 实际 trustedIPs、HTTPS issuer、secrets-encryption、生产独立节点池、Harbor 插件 artifact 和集群网络隔离仍未验收，旧文档不能代替现场状态。

## 后续门槛

1. 恢复并核实控制面 / etcd、备份、NetworkPolicy 控制器、存储与实际容量；按部署仓的准入步骤审核具体 GitOps 变更，逐环境落地。
2. 按实际已核实的可达 IP 执行默认拒绝与允许路径测试，再验证专属 PG/Redis 协议及 RLS。探针只证明网络策略，不能冒充真实 qian-platform。
3. 用户已批准 [S1-DEC-016](../../pm/tasks/S1-DEC-016.md)：DEP-001 先完成真实基础设施验收，真实平台四依赖协议联调由 DEP-005 的发布必过项承接，显式依赖 DEP-002，并经 DEP-004 依赖 DEP-003。该裁决不免除入口/加密、生产节点池或上线检查。

S1-DEP-001 保持 blocked，验收项均未勾选。本机验证与静态清单合并不等于 staging 或 prod 部署完成。

## 批准分阶段验收后的复核

2026-10-11 16:10（Asia/Shanghai）只读复核：API `/readyz` 成功，三节点 Ready；集群仅有 `qian-tools`，没有 `qian-staging` / `qian-prod`。现网 K3s secrets-encryption 为 Enabled；Traefik 的 web/websecure 入口已配置 trustedIPs；运维 Headlamp 的 OIDC discovery 返回 HTTPS issuer（这不代表尚未实现的 Qian 身份服务已验收）。local-path 实际节点路径为 `/data/k3s-storage`。三个节点均未设置 Qian 生产专用池标签，且正在承载其他工作负载。

旧 API 故障已不再作为当前唯一阻塞。下一步准备 staging 的固定版本、手动同步 GitOps 方案；生产专用节点需用户指定或提供新增节点，真实两环境隔离及应用发布门仍未验收。
