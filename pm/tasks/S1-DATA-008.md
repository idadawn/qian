---
id: S1-DATA-008
title: 原生 PostgreSQL 数据端口：受限角色、连接与事务租户隔离
status: doing
stage: S1
area: DATA
wave: 2
priority: P0
type: feature
owner: "cody"
estimate: 3d
depends: [S1-DEC-030, S1-DATA-001, S1-RT-033]
refs:
  - docs/decisions/gateway-native-aot-options.md
  - docs/adr/ADR-019-library-first-packages.md
updated: 2026-10-23
---

## 背景

负责人已在DEC-030确认方案A。交付NativeData独立Definition与PostgreSQL Provider，为gateway原生审计提供真实连接/事务；现有EF Data与平台/Scheduler装配保持原边界。

## 验收

- [ ] Definition / Provider、唯一组件清单、配置 Schema、PublicAPI、README 与依赖门齐备；服务键为 NativeData，不冒充 IDataService / Data，不修改既有 EF 模型安全检查
- [ ] IT：真实 PostgreSQL 受限登录、两租户的原始 SQL 读写隔离；拒绝超级用户、BYPASSRLS、角色切换、缺失/不匹配租户及未登记连接；声明的租户表缺少 FORCE RLS 或保护策略时拒绝装配
- [ ] IT：连接打开与事务开始均设置正确租户上下文；提交/回滚/取消/释放及连接池复用不泄漏前一租户，未结束事务随会话释放回滚，组件卸载撤销服务并释放活动会话
- [ ] IT：显式平台连接必须使用单独的受限登录角色及仍有效的 global 调用上下文，租户调用与角色成员关系不能冒充；凭据由受信任宿主绑定读取，不进入清单、错误或日志
- [ ] 固定 Linux 环境严格 NativeAOT 发布并运行真实 PostgreSQL 读写/事务/隔离探针，IL 警告不抑制；现有平台与 Scheduler 的 EF 路径回归通过

## 进度日志

- 2026-10-23 cody：创建任务
- 2026-10-23 cody：开始；方案A已获负责人明确批准并记录PR230，决策CI因系统依赖安装超时重跑。实现保留在独立分支，正式集成在决策及本任务CI全绿后依序进行；先冻结Definition端口再接原生Audit
