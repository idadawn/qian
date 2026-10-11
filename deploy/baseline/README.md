# S1-DEP-001 基线交付与现场状态

部署资产唯一源为部署仓 [PR #88](https://github.com/idadawn/iclaw-deploy/pull/88)：`2-qian/baseline`、`env-ds/qian-staging`、`env-ds/qian-prod`。Qian 仓库只记录任务与回链，不再复制一份生效清单。

2026-10-11 21:21（Asia/Shanghai），两环境基础设施真实验收均已通过，S1-DEP-001 按已批准 DEC-016 完成。真实平台四依赖应用联调继续作为 S1-DEP-005 的发布必过项。下文保留准备、阻塞与维护记录，最终结果见末节。

## 已交付物料与本机验证

- 两环境独立 namespace、PostgreSQL 16 / Redis 7 与独立 PVC；镜像按官方 registry 查询的多架构 digest 固定。
- default-deny ingress/egress；仅明确的应用/迁移 Pod 可连同环境 PG/Redis 与集群 DNS。RabbitMQ/Apollo 尚未放行，须专属实例与实际标签就绪后加入。
- ResourceQuota、LimitRange、PDB、非 root、只读根、资源限制与认证探针；生产数据服务强制指定独立节点池，不擅自复用现有节点。
- Secret 引导脚本只建缺失、不覆盖、不打印；清单里没有秘密值。PG 应用角色不能绕过 RLS，租户表仍须由各服务迁移启用并强制 RLS。
- 两个 overlay 可渲染，静态门及隔离/节点池/浮动镜像反例通过；本机固定镜像实际启动、PG 应用角色与双租户 RLS、Redis 哈希 ACL/匿名拒绝/认证探针通过。测试容器和临时凭据已清理。

## 初次现场阻塞（历史记录，已恢复）

2026-10-11 04:34–04:42（Asia/Shanghai），通过 qd 做只读检查，Kubernetes API 返回 `ServiceUnavailable` / `apiserver not ready`；K3s 服务处于 `activating/start`，两次观察的累计重启数为 550 / 551。另外两个控制面在本次有限等待内未返回状态，不能据此推断节点宕机。加密状态检查未获得有效结果。

初次只读核验时尚未执行集群/节点变更、创建命名空间或现场测试 Pod，两份基线 overlay 尚未加入 Argo Application。后续授权实施和现场验收见本页最新记录；旧文档不能代替现场状态。

## 后续门槛

1. 恢复并核实控制面 / etcd、备份、NetworkPolicy 控制器、存储与实际容量；按部署仓的准入步骤审核具体 GitOps 变更，逐环境落地。
2. 按实际已核实的可达 IP 执行默认拒绝与允许路径测试，再验证专属 PG/Redis 协议及 RLS。探针只证明网络策略，不能冒充真实 qian-platform。
3. 用户已批准 [S1-DEC-016](../../pm/tasks/S1-DEC-016.md)：DEP-001 先完成真实基础设施验收，真实平台四依赖协议联调由 DEP-005 的发布必过项承接，显式依赖 DEP-002，并经 DEP-004 依赖 DEP-003。该裁决不免除入口/加密、生产节点池或上线检查。

这份早期记录尚未完成现场验收。本机验证与静态清单合并不等于部署完成；后续实施以本页最新验收记录为准。

## 批准分阶段验收后的复核

2026-10-11 16:10（Asia/Shanghai）只读复核：API `/readyz` 成功，三节点 Ready；集群仅有 `qian-tools`，没有 `qian-staging` / `qian-prod`。现网 K3s secrets-encryption 为 Enabled；Traefik 的 web/websecure 入口已配置 trustedIPs；运维 Headlamp 的 OIDC discovery 返回 HTTPS issuer（这不代表尚未实现的 Qian 身份服务已验收）。local-path 实际节点路径为 `/data/k3s-storage`。三个节点均未设置 Qian 生产专用池标签，且正在承载其他工作负载。

旧 API 故障已不再作为当前唯一阻塞。下一步准备 staging 的固定版本、手动同步 GitOps 方案；生产专用节点需用户指定或提供新增节点，真实两环境隔离及应用发布门仍未验收。

### staging 调度容量复核

2026-10-11 16:10（Asia/Shanghai）读取节点 allocatable、Allocated resources 与 local-path 实际路径的磁盘余量，结果如下。CPU/内存为已声明请求后的剩余量，不是瞬时利用率；资源状态会变化，上线前须复核。

| 节点 | CPU 请求余量 | 内存请求余量 | local-path 磁盘可用 | 当前 PG 配置是否可放置 |
|---|---|---|---|---|
| qd | 55m | 约 342.6 GiB | 约 6.20 TiB（/data） | 否，CPU 不足 |
| k3s-worker01 | 995m | 约 372 MiB | 约 20.1 GiB（根分区） | 否，内存不足，卷增长空间也紧张 |
| k3s-worker02 | 445m | 约 498 MiB | 约 9.65 GiB（根分区） | 否，CPU、内存与卷空间不足 |

现有 PG 请求为 500m CPU / 512 MiB、PVC 20 GiB；Redis 为 100m / 256 MiB、PVC 8 GiB。合计至少需要可调度的 600m / 768 MiB 与 28 GiB 卷空间，并保留系统和运行余量。当前没有节点能承载 PG；不能凭瞬时空闲 CPU 或静态配额判定可部署。

上述表格是新生产节点加入前的共享池快照。用户随后明确选择现有 K3s 内的 Qian 专用节点；生产节点和基础设施已经按下节完成。staging 数据服务仍需额外可调度资源，没有降低数据库请求以绕过约束。

## 生产专用节点与真实数据平面（2026-10-11）

用户已确认“仍用现有集群，为 Qian 划出专用节点”。部署仓 [PR #89](https://github.com/idadawn/iclaw-deploy/pull/89) 已全绿合并：pve01 上新增 VM106 / qian-prod01（2 vCPU、4 GiB 内存、60 GiB 系统盘、独立 100 GiB 数据盘），仅作为 agent 加入现有集群，原控制面成员不变。

- 实际地址为 DHCP 分配的 192.168.1.185；VPN 10.78.1.185 管理访问已验证。密码、交互式认证和 root SSH 登录关闭，cloud-init schema 通过。
- 与现网同版 v1.35.5+k3s1；生产专用标签、NoSchedule taint 与外部 LB 排除标签均生效。`/data` 通过 UUID 挂载，agent 强制依赖挂载；已验证一次重启后自动恢复，四个节点全部 Ready。
- `qian-prod-baseline` 固定部署仓 revision `1d66d8a841bba9327f2466ab203b9762d08f3402`，手动同步成功且 Synced/Healthy，未开启自动同步、prune 或 selfHeal。
- PG/Redis 均 Ready，20 GiB / 8 GiB PVC 位于专属数据盘；生产请求为 600m / 768 MiB / 28 GiB，配额、LimitRange 和默认拒绝策略实际存在。
- 部署仓 [PR #90](https://github.com/idadawn/iclaw-deploy/pull/90) 的脚本已在生产完整执行：应用角色真实 TCP 认证、无租户零行及双租户 RLS、Redis 认证读写/匿名拒绝通过；授权对照可达 PG、其他 namespace DNS 与核实的公网端点，无权限 Pod 对三条路径均被拒绝。测试资源已清理。

这证明生产基础设施，不代表完整平台应用已部署或四依赖应用联调已通过。PVE 仍是与两个既有 worker 共用的物理宿主，不增加物理故障域；staging 数据服务容量仍需安排。

## staging 治理与网络验证（数据部署前快照）

部署仓 PR #90 已合并。`qian-staging-baseline` 同样固定基线 revision，未开启自动同步；仅选择同步 Namespace、ResourceQuota、LimitRange 和五条 NetworkPolicy，没有启动 StatefulSet 或创建数据卷。

`qian-staging` 的配额/默认限制已存在，默认拒绝策略已应用。运行 `d0d69259579e` 在 k3s-worker01 上完成真实网络对照：授权探针能访问跨 namespace DNS 与已核实公网端点，无权限探针对两项均被拒绝。报告明确为 `data_checked: false`；临时 Pod 和网络规则均已清除。首次准备镜像时镜像源返回 502；核对本地缓存的同一 OCI digest 后重新执行，未更改安全断言或网络规则。

staging Application 整体仍未完成同步，数据服务与 PVC 尚不存在；网络治理通过不代表 PG/Redis 或真实平台联调完成。

## staging 容量调整方案（已批准并执行）

可评审的最小调整为 pve01 的 VM101 / k3s-worker01：内存 8192 → 9216 MiB，系统盘 60 → 100 GiB；vCPU 保持 4 个，现有应用的请求不下调。增加的 1 GiB 内存可补足 PG/Redis 768 MiB 请求的缺口，磁盘扩展为两个数据卷及既有数据保留空间。执行前须按当时宿主余量重新核验，不能把本次快照当作永久容量保证。

该 VM 未启用 memory hotplug，NUMA 为 0，需要维护重启；实际系统盘为 `/dev/sda3` 上的 LVM/ext4，`/data/k3s-storage` 也位于该文件系统。worker01 上有单副本 CoreDNS、Traefik，以及 ASR/PDM 和本地卷相关业务。维护必须控制影响，不能直接重启了事。

执行门：

1. 批准维护窗口后，复核另外两个控制面/etcd、API、备份空间、PVE 内存预算与所有受影响工作负载；任一前置不满足即停止。
2. 保存 qd 本地 etcd 快照和 PVE 本地 VM101 的可恢复快照/备份，核验可用性；记录节点、卷、服务与入口基线。材料只保留在受保护的现有管理主机，不写入 Git、不输出凭据、不外发。此处用于维护回退，不冒充异机灾备。
3. 先确保 CoreDNS/Traefik 在其他可用节点有 Ready 实例；检查 PDB 与本地卷约束，对无法迁移的单副本业务使用已批准维护窗口。此步骤涉及既有服务，不包含在新生产 worker 的无迁移实施中。
4. 仅调整 VM101 的内存与 scsi0 容量；在停机/重启边界完成内存生效，并逐层验证磁盘、分区、PV、LV 和 ext4 的扩大，不操作其他磁盘、不缩盘。
5. 节点 Ready 后核验 etcd、DNS、入口、原有业务与 PVC；恢复调度，再复核 staging 数据服务实际可调度余量，最后通过固定 GitOps 版本部署并运行完整验收。

失败时停止后续部署，保留快照与诊断；按维护前快照制定恢复，不能通过直接缩盘、删除现有 PVC 或跳过业务恢复检查来回退。方案准备时未对 VM101 或既有应用执行变更；获批后的执行记录见下节。

## 已批准维护的执行结果

用户明确批准现在按 PR #39 方案执行。部署仓 [PR #91](https://github.com/idadawn/iclaw-deploy/pull/91) 记录 VM101 已完成内存 8→9 GiB、scsi0 60→100 GiB、分区/LVM/ext4 扩容和受控停机启动。数据目录可用约 58 GiB；现有请求之后余约 995m CPU / 1367 MiB，足以容纳本次 PG/Redis。

已保存 qd 本地 etcd 快照和 PVE 可回滚 VM 快照。首次完整备份带来高磁盘负载并伴随控制面短暂异常，已停止该备份，改用获批方案中的文件系统冻结/解冻快照；中断备份不作恢复依据。公网入口观察到两次短时超时，随后恢复；没有将本次维护描述为零停机。

21:12（Asia/Shanghai）原有 27 个活动 Pod 的容器均实际重新启动并 Ready，ASR/PDM 健康接口返回 200，原有两项本地 PVC 绑定不变。DNS/入口的临时副本均恢复为原数量，worker01 恢复调度，四节点 Ready。staging 的数据服务将固定落在完成扩容的 worker01，仍须真实部署和协议验收后才能关闭任务。

## 两环境基线最终验收

部署仓 PR #91 全绿合并后，staging 固定 revision `3fc24a6275b89a6b1fb80e32c2c8262815497c15` 手动同步成功；PG/Redis 在 worker01 Ready，20 GiB / 8 GiB PVC 位于其扩容后的数据目录。生产保持原固定 revision 和 qian-prod01 专用节点、独立数据盘。两个子 Application 均为 Synced/Healthy，没有开启自动同步、prune 或 selfHeal。

- staging 完整验收 `cefbdf7eac94`、生产维护后复测 `492f888bfdac` 均通过，报告均为 `data_checked: true`。
- PG 真实 TCP 认证、非特权角色及无租户/双租户 RLS，Redis 认证读写及匿名拒绝均通过。
- 授权对照可达 PG、其他 namespace DNS 和重新核实的公网端点；无权限 Pod 对三条路径均被拒绝。测试 schema、Pod 和临时网络规则已清理。
- 两 namespace 各自有 PG/Redis、Secret、PVC、ResourceQuota 和 LimitRange，实际镜像均固定 digest；生产节点池检查再次通过。
- Traefik trustedIPs、运维 OIDC 的 HTTPS issuer、K3s secrets-encryption Enabled 已重新核对。运维 OIDC 不能代替 Qian IdP 的真实 PKCE 验收。

镜像源曾返回 502，已通过相同摘要的生产镜像缓存完成 staging 启动，传输和 OCI 摘要均已核对，临时归档已删除。此次关闭的是基础设施基线；真实平台 PG/Redis/RabbitMQ/Apollo 联调保留在 DEP-005，身份联调保留在 IDP-002。
