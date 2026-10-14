---
id: S1-DATA-005
title: 数据连接连通测试的出网约束与凭据格式错误
status: doing
stage: S1
area: DATA
wave: 2
priority: P1
type: fix
owner: "claude"
estimate: 1d
depends: [S1-DATA-003]
refs:
  - docs/prd/07-s1-foundation.md#7.6
  - packages/Qian.Data/README.md
updated: 2026-10-15
---

## 背景

S1-DATA-003 交付后遗留两处：连通测试会从平台连到租户填写的任意主机，回环、云元数据地址与集群内部服务都可能被当作探测目标；租户密钥内容不是 UTF-8 文本时 PG 方言抛出未映射的异常，端点返回 500。本任务在 qian.data 内加一道出网检查：目标主机解析出的每个地址都须通过运维可配置的拒绝 / 放行网段，连接只用检查过的地址；并为凭据格式补结构化错误。完整的出口代理与 SSRF 基线仍按 PRD 7.17 归 S2。

## 验收

- [ ] UT：出网检查的缺省设置拒绝以下地址：回环、链路本地（含云元数据 169.254.169.254）、100.100.100.200、未指定地址、组播与广播地址。IPv4 映射的 IPv6 地址按 IPv4 判定。配置放行网段后，网段外的地址一律拒绝；拒绝网段优先于放行网段；网段写法无效时宿主启动即失败
- [ ] IT：`POST /connections/{id}:test` 的目标主机解析出的任一地址被拒时，返回 422 `DATA_CONNECTION_TARGET_DENIED`，且不发起任何连接（本机假服务端收不到连接）；放行时只连接检查过的地址，不二次解析
- [ ] IT：租户密钥内容不是 UTF-8 文本时，连通测试返回 422 `DATA_CONNECTION_CREDENTIAL_INVALID`，不再是 500

## 进度日志

- 2026-10-15 claude：创建任务
- 2026-10-15 claude：开始
