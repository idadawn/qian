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
3. DEP-001 要求真实平台四依赖联调，但 DEP-002（L1）与 DEP-003（镜像）又依赖它；验收顺序等待 [S1-DEC-016](../../pm/tasks/S1-DEC-016.md)。该裁决不能免除四依赖联调、实际入口/加密或上线检查。

S1-DEP-001 保持 blocked，验收项均未勾选。本机验证与静态清单合并不等于 staging 或 prod 部署完成。
