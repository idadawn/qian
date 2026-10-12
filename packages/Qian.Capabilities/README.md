# 平台能力目录

本目录是 Capabilities seam 的 Provider；Definition 在 `Qian.Capabilities.Abstractions`，HTTP 适配器在 `Qian.Capabilities.AspNetCore`。平台声明式表面位于 `plugins/qian.capabilities`，只装入 `platform`。按 R34，它不是消费者 standalone 中独立安装的组件；完整平台安装与私有包发布尚未验收，现有插件目录仍保留骨架标记。

## 当前实现

`CapabilitiesPlugin` 在 global scope 提供 `ctx.Capabilities()`。它依赖实际 Registry、租户主档和租户/授权/审计策略服务键；装载器撤销必要依赖后，该服务随生命周期撤销。目录读取 `GetScopeEntriesAsync` 同步后的实际 Ready 注册，再与当前 Runtime 的 `ExecutableCapabilities` 绑定取交集，不把预装声明当成可调用代码。

目录、提示词和调用每次共享同一个判定：已认证主体与当前租户一致、主档为 active/trial、scope 中的组件可见、OAuth scope 满足、真实 RBAC 允许该权限。令牌中的 roles 不授予权限。提示词额外要求 `agent.expose`；S1 尚不开放真实 Agent 委托执行。被隐藏、禁用、撤权或不存在的能力在调用表面统一为 `NOT_FOUND`，不会返回 Registry 的管理原因。

条目来自 SDK 生成的不可变 `CapabilityContract`，包括实际 JsonTypeInfo 派生 Schema、敏感字段、Agent 字段和共用 JCS/SHA-256 摘要。输出通过冻结 `capability-entry` 校验并检查嵌套输入租户字段，不包含内部 binding、配置或服务对象。当前返回 command/query；event/entity 聚合尚未交付。

`InvokeAsync` 调用同一绑定的真实 Mediator 请求，继续经过租户、权限与审计策略以及业务事务；目录返回值不是可缓存的执行授权。Provider 撤销会取消正在执行的请求，绑定本身也在卸载或 restrict 后拒绝缓存引用。宿主仍需按业务幂等规则处理可重试事务失败；通用绑定尚未复用 CRM 专用入口的整笔重试策略。

## HTTP 装配

宿主先安装经过验证的身份、租户与 ErrorDetail 中间件，再调用 `MapQianCapabilities`，提供从当前可信租户解析实际 Runtime 视图的函数。身份从请求服务的 `ICurrentUser` 获取，HTTP 输入不能指定主体或租户。注册身份适配器必须早于数据服务，防止匿名兜底占住框架身份端口。

- `GET /api/capabilities?kind=&q=`：返回 `{ items, ttlMs, cacheScope: "private" }`，同时设置私有且不存储的 HTTP 缓存指令。
- `GET /api/kernel/capabilities`：同一实现的 S1 别名，带 `Deprecation` 头。
- `POST /api/capabilities/{plugin}/{name}/invoke`：重新授权后反序列化真实请求并执行；请求体必须遵循生成的输入 Schema。

提示词是服务端 `ComposePromptAsync` 端口，未新增生产 HTTP 提示词接口。测试宿主的 `/api/acceptance/prompt` 仅作为该生产端口的真实认证入口胶水。

## 验收与保留项

`tests/Qian.Sample.Crm.Tests/CapabilityCatalogTests.cs` 使用真实 Kestrel、签名 S2S、受限 PostgreSQL、RBAC、Registry、SDK 绑定与客户业务。验证契约、别名、缺权限/scope、restrict、卸载、数据库 scope patch、撤权与冻结，并检查实际客户、Outbox 和审计。测试组合使用必要的实际 Provider，不声称已经覆盖默认 platform 全部 Provider；测试签发器也不替代真实 Identity 登录验收。

RT-006 的 OpenAPI 聚合与破坏性变更拒绝、版本化契约仓库、扩展点、菜单贡献及偏好仍未完成。QA-002 的完整 platform/minimal 等价与自动热重载最终验收仍保持未完成。RT-006 保持 doing，PR 保持草稿。
