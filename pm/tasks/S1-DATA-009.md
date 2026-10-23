---
id: S1-DATA-009
title: 修复原生连接池在并发打开与卸载后残留空闲连接
status: done
stage: S1
area: DATA
wave: 2
priority: P0
type: fix
owner: "cody"
estimate: 1d
depends: [S1-DATA-008]
refs:
  - packages/Qian.Data.Native.Postgres/README.md
  - pm/tasks/S1-DATA-008.md
updated: 2026-10-24
---

## 背景

PR251原始100轮/2秒门在Linux arm64限制2 CPU实测失败；新非池化观察连接额外10秒仍见同PID，诊断显示Npgsql DataSource已Dispose但仍有一个Ready空闲connector。保留原门禁和其他池隔离，定位并修复最小交接/清理缺口，不以重跑通过替代修复。

## 验收

- [x] 以准确依赖版本和固定交错建立原实现失败、修复实现通过的真实 PostgreSQL 红绿回归；只把已证实的边界写为根因
- [x] Linux 原打开/卸载 100 轮、每轮 2 秒观察门保持不变且通过；不跳过、不以增加等待或重跑成功代替修复
- [x] 修复仅作用于本组件拥有的连接池；其他池的活动连接与复用保持正常，不调用 ClearAllPools、不全局禁用池
- [x] NativeData 既有 17 项、直接消费者必要回归与严格 Linux NativeAOT 发布/真库门通过；无 IL 警告抑制
- [x] 测试显式捕获资源清理异常；失败证据包含独立非池化观察与连接池状态，不输出凭据或连接串

## 进度日志

- 2026-10-23 cody：创建任务
- 2026-10-23 cody：开始独立修复；核对Npgsql10.0.3准确源码与已有Linux诊断，先建立确定性红例，不改原100轮/2秒门
- 2026-10-23 cody：原现有数据测试在Linux固定交错复现Idle=1而Channel队列为空的残留；按每绑定容量门与延迟清理接管实现修复。固定红例失败/当前版通过；NativeData全项25/25、全仓dotnet1561/1561、Release构建和94项门禁通过，待Linux严格AOT CI复验。
- 2026-10-24 cody：已合入PR #254 (90c6893e79)；Linux固定10.0.13/10.0.3红绿回归、原100轮/2秒门、NativeData25项、全仓1561项及严格原生数据AOT门全过；租约清理异常有脱敏诊断。
