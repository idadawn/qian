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
- `GET /api/capabilities/openapi.json`：按同一权限过滤生成 OpenAPI 3.1，仅包含当前实际可调用绑定的执行路径和输入/输出 Schema；递归 Schema 引用重定位到各自组件。
- `POST /api/capabilities/{plugin}/{name}/invoke`：重新授权后反序列化真实请求并执行；请求体必须遵循生成的输入 Schema。

提示词是服务端 `ComposePromptAsync` 端口，未新增生产 HTTP 提示词接口。测试宿主的 `/api/acceptance/prompt` 仅作为该生产端口的真实认证入口胶水。

## 验收与保留项

`tests/Qian.Sample.Crm.Tests/CapabilityCatalogTests.cs` 使用真实 Kestrel、签名 S2S、受限 PostgreSQL、RBAC、Registry、SDK 绑定与客户业务。验证契约、别名、缺权限/scope、restrict、卸载、数据库 scope patch、撤权与冻结，并检查实际客户、Outbox 和审计。测试组合使用必要的实际 Provider，不声称已经覆盖默认 platform 全部 Provider；测试签发器也不替代真实 Identity 登录验收。

当前 OpenAPI 聚合覆盖 SDK 真实能力的调用入口，其他组件声明的 HTTP/gRPC 契约仍需接入版本化契约仓库。RT-006 的完整契约仓库、扩展点、菜单贡献及偏好仍未完成。QA-002 的完整 platform/minimal 等价与自动热重载最终验收仍保持未完成。RT-006 保持 doing，PR 保持草稿。

## 契约兼容检查

`IOpenApiCompatibilityProbe` 由 `OasDiffOpenApiProbe` 实现，使用固定的 [oasdiff 1.33.0](https://github.com/oasdiff/oasdiff/releases/tag/v1.33.0)，不以手写字段比较替代真实 OpenAPI 差异检测。下载脚本 `scripts/prepare_contract_tools.py --destination <工具目录>` 按仓库锁文件校验发行归档 SHA-256，只提取检查器普通文件。运行宿主需预装对应架构的二进制并把绝对路径交给 Probe；运行中不自动安装或更新。工具采用 Apache-2.0 许可证。

探测对基线和候选文档先做 OpenAPI 3.1 解析，再运行固定参数的兼容检查。外部 `$ref` 明确禁用；不调用 shell，不继承环境配置，使用独立空配置、受限输出和 30 秒超时。取消会终止子进程，临时契约文件在退出后删除。检测失败、版本不符和报告异常都抛错，不能视为兼容。

`OpenApiContractPlugin` 是平台装配器使用的普通包装，保持原业务插件依赖，在其 Apply 之前探测带 `/vN` 的同组件命名空间契约；破坏性变更导致挂载 Failed，业务注册不会公开。它不修改 Runtime，也不是调用者可绕过的请求参数。生产装配必须从已验证的版本化制品取得完整基线与候选，不能使用当前用户权限过滤后的 OpenAPI 返回值。注册表/发布准入的完整制品接线仍未完成，当前宿主需显式采用此包装。

测试通过 `QIAN_OASDIFF_PATH` 指向实际锁定工具；CI 会先校验下载，缺失工具不会跳过测试。相关测试覆盖说明与兼容响应扩展、输入类型破坏、路径删除、外部引用拒绝，以及真实客户组件挂载被拒/兼容恢复后实际执行。

OpenAPI 文档格式按 [OpenAPI 3.1.1 规范](https://spec.openapis.org/oas/v3.1.1.html) 生成，使用 Microsoft.OpenApi 3.10.2 解析验证。平台发行镜像携带工具及完整契约制品的验收尚未完成。
