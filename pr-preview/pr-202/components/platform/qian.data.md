> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# qian.data 数据存储默认安全装配

> 占位页：组件文件夹尚未建立。S1-DOCS-002 起本页由收集脚本从组件文件夹生成，按[组件页模板](/pr-preview/pr-202/design/prd/specs/docs-site.md)固定九节：用途、安装、配置、表面、API、示例、模型体验、已知限制与延期项、变更。

**阶段**：S1

RLS 拦截器、租户与软删过滤、审计字段、并发令牌与同库 Outbox 的默认装配。

## 设计依据

- [PRD §7.6](/pr-preview/pr-202/design/prd/07-s1-foundation.md)
- [ADR-004](/pr-preview/pr-202/design/adr/adr-004-database-tenant-isolation.md)
