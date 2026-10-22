---
id: S1-DATA-008
title: 原生 PostgreSQL 数据端口：受限角色、连接与事务租户隔离
status: review
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

- [x] Definition / Provider、唯一组件清单、配置 Schema、PublicAPI、README 与依赖门齐备；服务键为 NativeData，不冒充 IDataService / Data，不修改既有 EF 模型安全检查
- [x] IT：真实 PostgreSQL 受限登录、两租户的原始 SQL 读写隔离；拒绝超级用户、BYPASSRLS、角色切换、缺失/不匹配租户及未登记连接；声明的租户表缺少 FORCE RLS 或保护策略时拒绝装配
- [x] IT：连接打开与事务开始均设置正确租户上下文；提交/回滚/取消/释放及连接池复用不泄漏前一租户，未结束事务随会话释放回滚，组件卸载撤销服务并释放活动会话
- [x] IT：显式平台连接必须使用单独的受限登录角色及仍有效的 global 调用上下文，租户调用与角色成员关系不能冒充；凭据由受信任宿主绑定读取，不进入清单、错误或日志
- [ ] 固定 Linux 环境严格 NativeAOT 发布并运行真实 PostgreSQL 读写/事务/隔离探针，IL 警告不抑制；现有平台与 Scheduler 的 EF 路径回归通过

## 进度日志

- 2026-10-23 cody：创建任务
- 2026-10-23 cody：开始；方案A已获负责人明确批准并记录PR230，决策CI因系统依赖安装超时重跑。实现保留在独立分支，正式集成在决策及本任务CI全绿后依序进行；先冻结Definition端口再接原生Audit
- 2026-10-23 cody：Definition 提交 125834ea7f，Provider 与持续验证入口提交 6bf40daf7a；受信任宿主登记具名绑定，组件配置仅引用绑定名，租户/显式平台分别打开，服务键仅为 NativeData。固定迁移表达式逐字校验 PUBLIC restrictive FOR ALL 保护策略；没有修改既有 EF Provider 或模型检查。PublicAPI、README、真实生成 Schema/清单齐备，49 包依赖门通过，组件 check 无漂移，文档门通过。
- 2026-10-23 cody：最新真实 PostgreSQL 专项 17/17 通过（TestResults/native-data-17）；包含两租户原始 SQL、受限登录/成员关系/权限与策略漂移、平台有效 global 入口及 tenant entry 的 Scoped(Global) 拒绝、外部真实 SERIALIZABLE 事务、提交预取消封存、事务回滚/连接池/advisory lock 清理、100 轮打开连接与卸载交错。卸载等待在途连接交接并清理，已拿到连接的异常路径也有释放范围；取消提交不得恢复会话继续读写。
- 2026-10-23 cody：6bf40daf7a 在固定 SDK 镜像严格发布 linux-arm64，IlcTreatWarningsAsErrors=true、TrimmerSingleWarn=false，TestResults/native-data-linux-publish-final.log 无 IL 警告。实际原生产物真库运行 1/1（TestResults/native-data-aot-final）；CI 同款直接 Linux 进程 driver 再验 1/1（TestResults/native-data-ci-driver），均未跳过。f9f730ce32 据此登记本组件 x-aot:true；正式包源安装未验收，x-skeleton:true 保留，未更改 gateway 整体 AOT 声明。
- 2026-10-23 cody：既有 EF DataSafetyTests 与 CrossHostTenantModelTests 13/13 通过（TestResults/native-data-ef-regression.xml）。最后一条验收仍未勾选：当前分支基线不含 Scheduler 实现，需父代理在串行整合树完成 Scheduler EF 路径回归及远端 CI 后收口，不能用原生单组件证据替代完整组合。
- 2026-10-23 cody：Provider 与严格原生运行证据已提交供 Audit 接入；前四项完成，等待串行整合树 Scheduler EF 回归与远端 CI 后最终收口
