---
id: S1-DATA-009
title: 修复原生连接池在并发打开与卸载后残留空闲连接
status: doing
stage: S1
area: DATA
wave: 2
priority: P0
type: fix
owner: "cody"
estimate: 1d
depends: [S1-DATA-008]
refs: []
updated: 2026-10-23
---

## 背景

PR251原始100轮/2秒门在Linux arm64限制2 CPU实测失败；新非池化观察连接额外10秒仍见同PID，诊断显示Npgsql DataSource已Dispose但仍有一个Ready空闲connector。保留原门禁和其他池隔离，定位并修复最小交接/清理缺口，不以重跑通过替代修复。

## 验收

- [ ] （待补充）

## 进度日志

- 2026-10-23 cody：创建任务
- 2026-10-23 cody：开始独立修复；核对Npgsql10.0.3准确源码与已有Linux诊断，先建立确定性红例，不改原100轮/2秒门
