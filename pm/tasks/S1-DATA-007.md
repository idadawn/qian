---
id: S1-DATA-007
title: 出网检查缺省值：开发环境放行回环，缺省拒绝 K3s 集群网段（R79 补记）
status: doing
stage: S1
area: DATA
wave: 2
priority: P1
type: fix
owner: "claude"
estimate: 0.5d
depends: [S1-DATA-005]
refs:
  - docs/decisions/key-decisions-v0.md
  - packages/Qian.Data/README.md
updated: 2026-10-16
---

## 背景

S1-DATA-005 的出网检查有两处缺省值不合适：

- 缺省拒绝回环，宿主在本机开发时要连本机数据库做连通测试，得手动从 `Egress.BlockedNetworks` 移掉两项；
- K3s 集群内部网段（Pod 10.42.0.0/16、Service 10.43.0.0/16）缺省没拦，原计划等部署任务在部署配置里追加。

2026-10-16 产品负责人确认 R79：进程内出网检查提前到 S1；完整出口代理与 SSRF 基线仍按 PRD 7.19 归 S2；生产环境仍靠 NetworkPolicy。同时要求这两处现在就改：开发环境缺省放行回环，K3s 默认网段并入缺省拒绝列表。

## 验收

- [ ] UT：开发环境与回环
  - 宿主环境为 Development 时，缺省放行 127.0.0.0/8 与 ::1；
  - 其他环境、以及没有登记 IHostEnvironment 的宿主，仍拒绝回环；
  - Development 下其余缺省拒绝项照常生效（链路本地、云元数据、K3s 网段等）；
  - 宿主显式把回环加回拒绝列表时以显式配置为准，无论这条配置登记在 AddQianDataConnections 之前还是之后。
- [ ] UT：K3s 网段
  - 缺省拒绝 10.42.0.0/16 与 10.43.0.0/16，含 IPv4 映射写法；
  - 相邻地址（10.41.255.255、10.44.0.1）不受影响；
  - 集群使用自定义网段时，运维可移除这两项、追加自己的网段。
- [ ] IT：连通测试端点
  - Development 宿主对本机假服务端的连通测试能通过出网检查并真正连上（假服务端收到连接）；
  - 同一宿主对 169.254.169.254 与 10.43.0.1 仍返回 422 DATA_CONNECTION_TARGET_DENIED；
  - Production 宿主对回环与 K3s 网段目标返回 TARGET_DENIED，且不发起连接。
- [ ] 文档同步新缺省值：Qian.Data 与 Qian.Data.AspNetCore 的 README、data-contracts §8、CHANGELOG，以及 key-decisions 的 R79 补记。

## 进度日志

- 2026-10-16 claude：创建任务
- 2026-10-16 claude：开始
