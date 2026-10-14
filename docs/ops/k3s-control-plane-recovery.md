# K3s 控制面故障处置方案（S1-DEP-007）

2026-10-13 在 S1-DEC-019 R73 复核时发现，现网 K3s 的控制面自 2026-10-11 起持续崩溃重启，对外表现为 apiserver 间歇返回 ServiceUnavailable。本文给出证据、根因、方案选择与执行步骤。执行需要 qd、pve01 与两台 worker 的 root 权限，并需产品负责人审核；Claude 只做过只读核验。

## 1 现象与证据（2026-10-13 只读核验）

| 事实 | 数值 / 内容 | 来源 |
|---|---|---|
| qd 的 `k3s.service` 累计重启 | 1118 次（10-11 25 次、10-12 204 次、10-13 截至 15:10 UTC 148 次） | `systemctl show k3s -p NRestarts`、journal |
| 每次退出的原因 | kube-controller-manager 报 `leaderelection lost`，进程以状态 1 退出，systemd 拉起 | `journalctl -u k3s` |
| 起点 | 2026-10-11 17:05 UTC（北京时间 10-12 01:05），一次 k3s 停止再启动之后 | journal 中 unit stopped 与 Starting 的时间 |
| etcd 在 qd 本地读 lease 键 | 1.1 到 4.7 秒，期望 0.1 秒；`slow_apply` 单次运行内累计 1879 | etcd 日志、`127.0.0.1:2381/metrics` |
| qd 到 worker01 的 etcd 对端延迟 | 平均约 210 ms，发送失败 132 次；到 worker02 平均 5 ms | `etcd_network_peer_round_trip_time_seconds`、`etcd_network_peer_sent_failures_total` |
| qd 看到的对端状态 | `dial tcp 192.168.1.163:2380: connection refused` 52 次、`192.168.1.164:2380` 24 次（14:20 到 15:10 UTC） | etcd 日志 |
| 节点间网络 | ping 往返 0.4 ms 左右，无丢包 | `ping` |
| qd 本机 | 8 核、385 GiB 内存、NVMe 系统盘；I/O 等待为 0，内存压力为 0，etcd 库 75 MB，WAL fsync 平均约 1 ms | PSI、vmstat、etcd 指标 |
| qd 上的其他负载 | 两个 GitHub Actions runner 无 CPU / 内存上限；一个常驻进程 `cubemaster` 32 天占满一核 | `systemctl show`、`ps` |
| worker01 / worker02 | 同在 Proxmox 宿主 pve01 上的 VM；pve01 为 8 逻辑 CPU、约 31 GiB 内存 | 部署仓库 `docs/ops/qian-prod-nodepool.md` |
| 10-11 的变更 | worker01（VM101）内存从 8192 调到 9216 MiB 并重启；新建 qian-prod01（VM106，4096 MiB）；此前 pve01 可用内存约 7.8 GiB | 部署仓库 `docs/ops/qian-staging-capacity.md` |

「connection refused」说明对端 VM 在线但 etcd 进程不在监听，即 worker 上的 k3s 也在反复重启；三个 etcd 成员中任意两个同时不在，集群就失去多数，apiserver 不可用，controller-manager 续约失败，三台 server 的 k3s 一起退出、拉起，循环往复。

## 2 根因

已确认的事实：三台 server 一起崩溃循环；qd 看到两台 worker 的 etcd 端口反复拒绝连接；qd 自身的磁盘、内存、网络正常。

最可能的触发原因（待阶段 0 第 1 步验证）：etcd 三成员里两个（worker01、worker02）跑在同一台已接近内存上限的 Proxmox 宿主 pve01 上。10-11 的两项变更之后，pve01 的可用内存按文档估算只剩约 2.5 GiB，VM 一旦被宿主压力拖慢，其 etcd 成员就跟不上心跳，触发选举；选举期间 apiserver 失去存储，controller-manager 丢失领导权，k3s 整进程退出。qd 自身是健康的，它的重启是被动的。「宿主压力是触发原因」目前是从 pve01 的内存余量与变更时间推断的，拒绝连接只能证明那一刻对端没有监听；必须把 worker 的 k3s 日志与 pve01 的 PSI、OOM、交换、I/O 记录按时间对齐后才算确认。方案 B 不依赖这个确认：无论触发原因是什么，把控制面从 pve01 的 VM 上拿走都是必要的。

加剧因素：qd 既是控制面，又承载两个无上限的 CI runner 和一个满占一核的常驻进程；Kun 的 CI 在 qd 上串行跑 24 个 job，10-12 16 时的 etcd 风暴与当天 Kun CI 时间重合。

结构性问题：两台 worker 同宿主，这个三成员 etcd 并不提供宿主级高可用，只把一台宿主的内存压力放大成整个集群的故障。

## 3 方案选择

| 方案 | 做法 | 能否彻底解决 | 代价 |
|---|---|---|---|
| A 保持三成员，扩 pve01 | 给 pve01 加内存，调大两台 worker 的 vCPU / 内存，关闭 balloon | 不能。两台 worker 仍同宿主，宿主任何抖动仍会带崩控制面 | 需要采购或停机加内存 |
| B qd 单控制面，三个 agent（推荐） | qd 独占 etcd / apiserver / controller；worker01、worker02、qian-prod01 都作 agent | 能。控制面落在最强、最稳的物理机上，不再跨 VM 做 raft 共识 | 一次约 1 小时的维护窗口；失去控制面高可用，但现状本就没有 |
| C 把 etcd 成员放到三台物理机 | 需要两台额外物理机 | 能 | 现网没有可用物理机 |

**推荐 B**。S1 阶段是试点集群，apiserver 短暂不可用不影响已运行的 Pod；qd 重启时控制面会中断几分钟，可接受。方案 B 之后，worker 的 VM 抖动只影响其自身的 Pod，不再影响控制面。

## 4 执行步骤

执行前提：产品负责人批准方案 B；执行人同时有 qd、pve01、worker01、worker02 的 root；选择覆盖两台 worker 全部工作负载的维护窗口（计划约 1 小时，实际以恢复验收为准）；执行期间暂停 Kun 的 CI runner。

### 阶段 0：止血（当天，约 30 分钟，不改集群拓扑）

1. 在 pve01 上确认宿主压力并记录：

   ```bash
   free -m; vmstat 1 5; swapon --show; qm list; for v in $(qm list | awk 'NR>1{print $1}'); do echo "== $v"; qm config $v | grep -E '^(memory|balloon|cores|sockets|name)'; done
   ```

   同时检查 pve01 是否有每小时整点的备份或快照计划（10-13 的 etcd 风暴在 01、02、03、05 时的 01 到 04 分成片出现）：`cat /etc/pve/jobs.cfg`、`systemctl list-timers`。有则先停。

   根因验证：把三处时间线对齐后写进 S1-DEP-007，确认或推翻「宿主压力触发」。

   ```bash
   # worker01 / worker02 各执行：重启次数与每次 leaderelection lost 的时间
   systemctl show k3s -p NRestarts; journalctl -u k3s -q --since "2026-10-11" --no-pager | grep -E "leaderelection lost|Started k3s.service" | cut -c1-15 | uniq -c | tail -40
   # pve01：压力、OOM、交换与 I/O 的历史记录
   cat /proc/pressure/cpu /proc/pressure/memory /proc/pressure/io; dmesg -T | grep -iE "oom|out of memory" | tail; swapon --show; journalctl -k --since "2026-10-11" | grep -iE "i/o error|blocked for more than" | tail; pvesh get /nodes/$(hostname)/rrddata --timeframe week | head -50
   ```

2. 在 qd 上给两个 runner 加 cgroup 上限，并把 Kun 的 runner 停到阶段 1 结束：

   两个 runner 放进同一个 slice，合计不超过 4 核、48 GiB；不要各设一份上限，那样合计仍可占满 8 核。

   ```bash
   printf '[Slice]\nCPUQuota=400%%\nMemoryMax=48G\nIOWeight=50\n' > /etc/systemd/system/actions-runners.slice
   for u in actions.runner.CavaiLAB-kun.qd-kun.service actions.runner.idadawn-iclaw.qd.service; do mkdir -p /etc/systemd/system/$u.d; printf '[Service]\nSlice=actions-runners.slice\nNice=10\n' > /etc/systemd/system/$u.d/10-slice.conf; done
   systemctl daemon-reload; systemctl restart actions.runner.idadawn-iclaw.qd.service; systemctl stop actions.runner.CavaiLAB-kun.qd-kun.service; systemctl show actions-runners.slice -p CPUQuotaPerSecUSec -p MemoryMax
   ```

3. 在 qd 上弄清 `/usr/local/services/cubetoolbox/CubeMaster/bin/cubemaster` 是什么、谁在用；不需要就停掉，需要就限制到一核以内。不要盲目 kill。

4. 在 qd 上保存 etcd 快照，连同 server token 与配置一起打包，记录 SHA-256，并复制到 qd 之外（执行人的工作机或备份盘，权限 0600，绝不进 Git）。按 k3s 官方说明，把快照恢复到新节点时必须使用原集群的 token，快照里的引导数据用它加密；缺 token 的快照恢复不了。后面每一步都以这份备份为回退点：

   ```bash
   k3s etcd-snapshot save --name dep007-pre-reset
   S=$(ls -t /var/lib/rancher/k3s/server/db/snapshots/dep007-pre-reset* | head -1)
   tar -czf /root/dep007-backup.tgz "$S" /var/lib/rancher/k3s/server/token /var/lib/rancher/k3s/server/node-token /etc/rancher/k3s/config.yaml /etc/rancher/k3s/registries.yaml
   chmod 0600 /root/dep007-backup.tgz; sha256sum "$S" /root/dep007-backup.tgz
   # 然后从执行人的工作机取走：scp root@qd:/root/dep007-backup.tgz <qd 之外的位置>/
   ```

阶段 0 止血后观察约 1 小时，记录 NRestarts 的增量与压力时间线。runner 限额只改变 qd 主机负载，不能单凭重启放缓证明 pve01 的触发原因；方案 B 的结构性理由独立于这项因果验证。

### 阶段 1：改为 qd 单控制面（维护窗口约 1 小时）

0. 前置核对与影响范围评估。

   逐台核实（两台 worker 各执行一次），卸载脚本会停止该节点全部 Pod、清空镜像缓存并删除 k3s 自己的数据目录，数据安全不能只凭「PV 路径在 /data」这一条推断：

   ```bash
   grep -E "data-dir|default-local-storage-path" /etc/rancher/k3s/config.yaml; systemctl cat k3s | grep -E "ExecStart|data-dir"; grep -nE "rm -rf" /usr/local/bin/k3s-uninstall.sh; findmnt /data; ls /data/k3s-storage; du -sh /data/k3s-storage
   ```

   逐台解析实际 `data-dir`、卸载脚本的删除函数，以及 PV 的真实路径和挂载来源。K3s 自身数据目录被卸载清理是正常行为，判断重点是业务卷是否落在清理范围内（包括符号链接与 bind mount）。PV 路径、挂载来源与基线不符，数据盘缺失或备份不完整时立即停止，不能自动改走其他路径。两台 worker 的 `/data/k3s-storage` 当前在根分区 LVM/ext4 上，并非独立挂载，须与 `deploy/baseline/README.md` 的已验收基线核对。数据前提通过后优先就地切换到既有 agent unit，保留 node password、业务卷与镜像缓存。

   业务数据备份（动任何节点之前）：对 qian-staging 的 PostgreSQL 做一次 `pg_dump`，对 Redis 执行 `SAVE` 后复制 RDB；把两台 worker 的 `/data/k3s-storage` 各 rsync 一份到 qd 之外；备份 `/etc/rancher/k3s/*.yaml`。两台 worker 上有按主机名钉定的数据卷，节点名必须保持 `k3s-worker01` / `k3s-worker02` 不变。

   影响范围（2026-10-13 15:40 UTC 实查），维护窗口要按下表的全部工作负载评估并通知各负责人，不能只按 qian-staging：

   | 节点 | 运行中的工作负载 | 钉在该节点的数据卷 |
   |---|---|---|
   | k3s-worker01 | qian-staging 2、pdm 3、safe-ai 1、iclaw 1、higress-gateway 1 副本、kube-system 1 | qian-staging 2、pdm 1、higress-system 1 |
   | k3s-worker02 | iclaw-staging 3、iclaw 3、higress-system 3（含 higress-gateway 1 副本、maas-gateway-redis 1 副本）、apollo-prod 2（admin / config 各 1 副本）、harbor-nginx 1、pdm 1、kube-system 2 | iclaw-staging 3、higress-system 1 |

   顺序与缓解：一次只改一台，先 worker01 后 worker02，每台约 10 到 15 分钟的 Pod 中断；Higress 网关与 Apollo 生产服务各保留另一副本在线，Traefik 入口在 qd 上不受影响；改 worker02 之前先 `kubectl cordon k3s-worker02` 并删除其上的 harbor-nginx Pod，让 Harbor 入口（VIP .201）搬到 qd，Harbor 的有状态部分本来就在 qd。重装后的镜像从 daocloud 镜像源重新拉取，不经过 Harbor，不会形成死锁。

1. 共同暂停两台 worker 的旧 server 服务，以便重置 qd 成员列表。`stop` 不等于移除 etcd 成员；这是共同的控制面暂停，后续业务节点改装仍逐台执行。先核实 unit 的 `KillMode` 和实际容器状态，此时不运行 killall / 卸载脚本，不删除 Node 对象。

   ```bash
   systemctl stop k3s
   systemctl mask --runtime k3s
   ```

2. 在 qd 上把 etcd 重置为单成员，再正常启动：

   ```bash
   systemctl stop k3s && k3s server --cluster-reset && systemctl start k3s && sleep 30 && kubectl get --raw /readyz && kubectl get nodes
   ```

   执行 reset 时保留原 server unit / EnvironmentFile 的必要参数，不能假定参数全部在 config.yaml；qd 的原 unit 含 secrets-encryption、node-ip、advertise-address 和 tls-san。明确提示重置完成后，再由原 unit 不带 reset 参数启动。使用带超时的 readyz 和 etcd 成员检查确认 qd 独立健康，失败则执行 §5，并停止后续 worker 改装。

3. 保存 Node / PV 基线并安全准备 agent 加入材料。不同时删除两台 Node；token 只通过受保护的文件或流传递，不回显到终端或写入任务日志。就地改装保留原 node password 和 Node UID；若选择经核实的卸载重装，仅在轮到对应节点时删除其 Node，以清除旧 node password Secret。

4. 两台 worker 按第 0 步定的顺序逐台改装为 agent（先 worker01，确认其 Pod 与数据卷恢复后再做 worker02）。 就地改装使用既有 agent unit / EnvironmentFile，保留原节点名、node-ip、node password 和业务卷；备份并移开旧 server 专用配置，禁用旧 server unit，启用 agent unit。无需卸载或删除 server 数据目录；已有 unit 带 node-ip 时不要重复写进 YAML，StringSlice 可能合并成两个 IPv4。切换后只移除旧 control-plane / etcd 角色标签，保留其他标签、污点与调度状态。下面的安装示例仅适用于完整核实后的卸载重装路径。先卸载 server 安装（第 0 步已核实它只删 k3s 自身目录，不碰 `/data`），再按部署仓库的 agent 模式安装，配置文件照抄 [qian-prod-agent.yaml](https://github.com/idadawn/iclaw-deploy/blob/main/0-cluster/qian-prod-agent.yaml) 的形式，把 `node-name` 改为原节点名，去掉 prod 的标签与污点，保留原有标签：

   ```bash
   /usr/local/bin/k3s-uninstall.sh; mkdir -p /etc/rancher/k3s; install -m 0600 /dev/stdin /etc/rancher/k3s/agent-token <<< '<node-token>'; printf 'server: https://192.168.1.161:6443\ntoken-file: /etc/rancher/k3s/agent-token\nnode-name: k3s-worker01\n' > /etc/rancher/k3s/config.yaml; curl -sfL https://get.k3s.io | INSTALL_K3S_VERSION=v1.35.5+k3s1 sh -s - agent
   ```

   `registries.yaml` 按部署仓库的 [qian-prod-registries.yaml](https://github.com/idadawn/iclaw-deploy/blob/main/0-cluster/qian-prod-registries.yaml) 恢复。版本固定为现网的 v1.35.5+k3s1，不顺手升级。

5. 在 qd 上确认三个 agent 回来、数据卷重新绑定、staging 工作负载恢复：

   ```bash
   kubectl get nodes -o wide; kubectl get pv | grep -E 'k3s-worker0[12]'; kubectl get pods -A -o wide | grep -vE 'Running|Completed'
   ```

6. 恢复 Kun 的 runner：`systemctl start actions.runner.CavaiLAB-kun.qd-kun.service`。

### 阶段 2：加固（1 天内）

- qd 的 kubelet 加系统预留，给控制面留出余量：先核对当前 Pod 请求与可分配资源，再合并 kubelet-arg。2026-10-14 核验 qd CPU 请求为 7945m/8000m，直接预留 3 核会影响既有 Pod 准入，因此此次采用 `system-reserved=memory=8Gi` 与 `kube-reserved=memory=4Gi`；CPU 预留须在释放请求或增加容量后另行评估，不下调既有业务请求。重启 k3s（会有一次约 1 分钟的 apiserver 中断，选低峰）。
- 阶段 0 的 `actions-runners.slice` 保留为长期配置：两个 runner 合计不超过 4 核、48 GiB；长期看把 runner 迁出控制面主机。
- pve01 的内存分配总和保持不超过宿主内存减 4 GiB，关闭所有 VM 的 balloon；本次测得的数值写进部署仓库 `docs/ops/qian-prod-nodepool.md`。
- etcd 快照确认落到 qd 之外（现有 `k3s-db-backup.timer` 每天 02:00 UTC，核对目标位置与保留策略）。
- 在 qd 上加一个每 5 分钟的只读健康检查定时器，把 `NRestarts`、`/readyz` 与 etcd `slow_apply` 写进 journal，连续两次异常就通知值班人；具体通道按运维现状选。
- 部署仓库 `0-cluster/README.md` 的 L0-1 现网基线从「3 节点全 control-plane+etcd」改为「qd 单控制面 + 3 个 agent」；本仓 `deploy/baseline/README.md` 同步。

### 阶段 3：验收（24 小时）

```bash
for i in $(seq 1 288); do date -u +%FT%TZ; systemctl show k3s -p NRestarts; kubectl get --raw /readyz; echo; sleep 300; done | tee /var/tmp/dep007-24h.log
```

稳定后清理崩溃循环的残留：harbor 命名空间在 qd 上有 3 个处于 Failed 的旧副本 Pod（core、jobservice、registry 各 1），确认新副本 Running 后删除。

通过标准见 S1-DEP-007 的验收：24 小时内 `NRestarts` 不再增长，`/readyz` 全部成功，etcd 日志不再成片出现 `apply request took too long`；结果回写 S1-DEP-001 与 S1-DEC-019。

## 5 回退

- 阶段 1 第 2 步失败：先停止 qd 的 k3s 并确认没有 server 进程运行，核对备份 SHA-256、原 token、配置与原 unit 参数，再恢复；`k3s server --cluster-reset --cluster-reset-restore-path=<阶段 0 的快照路径>`，再正常启动。
- 阶段 1 第 4 步失败（worker 加不进来）：控制面已在 qd 上独立运行，不影响 prod；worker 可反复重装 agent，数据卷在 `/data/k3s-storage` 不受影响。
- 任何时候不要把 worker 重新装回 server：那等于回到故障前的拓扑。

## 6 禁止事项

- 不在故障期间升级 k3s、轮换 secrets-encryption 密钥或改 Traefik / MetalLB。
- 不删除任何 VM、不清空 `/data`。
- 不用 `kubectl delete node` 处理 qian-prod01：它本来就是 agent，与本故障无关。
- 不要在 qd 上不加时间窗地跑 `journalctl -u k3s`：两天的日志有上百万行，会满占一核数分钟。

## 7 留痕

执行人把阶段 0 的测量值、阶段 1 的快照名与各步时间、阶段 3 的日志摘要写进 S1-DEP-007 的进度日志；部署仓库的变更走 PR 并在 S1-DEP-007 回链。

## 8 执行结果（2026-10-14）

方案 B 已执行。qd 于2026-10-13T17:19:39Z完成单成员重置，17:19:49Z readyz成功；两台worker以原UID和节点名成为agent，server unit永久masked，所有业务卷保留。阶段0至2证据、发生的配置重复修复、旧Pod回收及实际停机时点均在 [S1-DEP-007](../../pm/tasks/S1-DEP-007.md)。原约1小时是估计，实际受I/O、重启修复、驱逐回收和重新调度影响超过该估计。

宿主内存不足未被确认：MemAvailable约6023MiB、memory PSI近零。进一步测量发现机械RAID上VM105持续读取；临时读限额1200→200帮助恢复后，已回到原值，未永久改变该服务预算。运行中采样不冒充完整硬件性能测试。

日常备份已从过时SQLite逻辑改为etcd直连、原token/配置加密异机传输，端到端解密与离线恢复结构验证通过；详细脚本与权限边界唯一源为部署仓的 `0-cluster/ops/README.md`。五分钟采样与每小时验收复核已启用，最终验收仍未完成。

2026-10-14 只读验收发现，原窗口内09:01 UTC与09:49至09:50 UTC分别集中出现9条与52条etcd慢请求，最长5.448秒。qd与两台worker agent未重启、五分钟readyz采样全部成功，但这不满足无成片慢请求的条件，不能按原2026-10-14T18:23:34Z截止关闭任务。恢复后的首个正常采样为2026-10-14T09:58:59.776802Z，新连续24小时最早截止2026-10-15T09:58:59.776802Z（北京时间10-15 17:58:59.776802）；若再出现重启、健康失败或成片慢请求，按恢复后的首个正常采样重新计算。健康脚本的`ok`只覆盖控制面与节点状态，不包含慢请求；验收必须另查`slow_apply_in_interval`和有界journal。触发原因尚未确认，本轮没有改变现网配置。
