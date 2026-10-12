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

当前 OpenAPI 聚合覆盖 SDK 真实能力的调用入口，其他组件声明的 HTTP/gRPC 契约仍需接入版本化契约仓库。RT-006 已提供静态协议与真实 gRPC 反射探测，完整安装发布流程接线和非 SDK 接口聚合仍未完成；菜单贡献及偏好已接入，范围与验证见下节。扩展点按 ADR-020 D20.3 和组件规格 §3.6 使用既有事件定义及可逆服务注册，不另建旧版独立扩展点注册表。QA-002 的完整 platform/minimal 等价与自动热重载最终验收仍保持未完成。RT-006 保持 doing，PR 保持草稿。

## 契约兼容检查

`IOpenApiCompatibilityProbe` 由 `OasDiffOpenApiProbe` 实现，使用固定的 [oasdiff 1.33.0](https://github.com/oasdiff/oasdiff/releases/tag/v1.33.0)，不以手写字段比较替代真实 OpenAPI 差异检测。下载脚本 `scripts/prepare_contract_tools.py --destination <工具目录>` 按仓库锁文件校验发行归档 SHA-256，只提取检查器普通文件。运行宿主需预装对应架构的二进制并把绝对路径交给 Probe；运行中不自动安装或更新。工具采用 Apache-2.0 许可证。

探测对基线和候选文档先做 OpenAPI 3.1 解析，再运行固定参数的兼容检查。外部 `$ref` 明确禁用；不调用 shell，不继承环境配置，使用独立空配置、受限输出和 30 秒超时。取消会终止子进程，临时契约文件在退出后删除。检测失败、版本不符和报告异常都抛错，不能视为兼容。

`OpenApiContractPlugin` 是平台装配器使用的普通包装，保持原业务插件依赖，在其 Apply 之前探测带 `/vN` 的同组件命名空间契约；破坏性变更导致挂载 Failed，业务注册不会公开。它不修改 Runtime，也不是调用者可绕过的请求参数。生产装配必须从已验证的版本化制品取得完整基线与候选，不能使用当前用户权限过滤后的 OpenAPI 返回值。宿主可用 FromRepository 选择已捕获的精确版本契约，候选版本必须与实际装载条目一致。完整在线注册表/发布准入仍未完成，当前宿主需显式采用此包装。

测试通过 `QIAN_OASDIFF_PATH` 指向实际锁定工具；CI 会先校验下载，缺失工具不会跳过测试。相关测试覆盖说明与兼容响应扩展、输入类型破坏、路径删除、外部引用拒绝，以及真实客户组件挂载被拒/兼容恢复后实际执行。

OpenAPI 文档格式按 [OpenAPI 3.1.1 规范](https://spec.openapis.org/oas/v3.1.1.html) 生成，使用 Microsoft.OpenApi 3.10.2 解析验证。平台发行镜像携带工具及完整契约制品的验收尚未完成。

## 版本化契约仓库

Capabilities 同时提供 `Contracts` 服务键（`ctx.Contracts()` / `IContractRepository`）。仓库只读取实际 Registry 预装集合中由 `FromArtifact` 核验并捕获的文件。组件的 metadata 表面以 `contracts.json` 声明契约索引；索引引用原有文件，不形成第二份插件清单：

```json
[
  { "id": "qian.sample.crm.api/v1", "kind": "openapi", "path": "./contracts/http-v1.openapi.json" },
  { "id": "qian.sample.crm.wire/v1", "kind": "proto", "path": "./contracts/customer-v1.proto" },
  { "id": "qian.sample.crm.item/v1", "kind": "json-schema", "path": "./schemas/customer-v1.schema.json" }
]
```

契约 ID 必须属于本组件命名空间并带 `/vN`；文件必须在同一制品内，不能越界、缺失或重复声明同一 ID。OpenAPI 支持共用读取器的 JSON/YAML 语法并验证 3.1 文档；JSON Schema 使用 2020-12 元 Schema 校验；proto 原始文件按制品形成封闭依赖集合；Provider 在公开契约仓库前执行真实编译，缺少检查器、语法错误或导入缺失均失败。实际服务还须经过带认证的反射一致性探测。

按组件 ID、精确制品版本和契约 ID 查询，返回原始内容的副本、文件 SHA-256 与制品目录摘要。仓库不按业务用户裁剪契约，也没有匿名 HTTP 读取入口；卸载后旧服务引用失效，在途兼容检查会取消。`CheckOpenApiAsync` 只比较两个完整版本制品中的同族 OpenAPI，缺基线、候选或检查器均拒绝。

`CapabilityContractCompiler.CreateOpenApi` 在构建期从一个组件的完整生成契约派生文档；目录和该编译器复用同一条目校验及哈希实现。真实客户验收宿主已使用生成契约构建三个有摘要的版本制品，再经 `FromRepository` 验证破坏性修订拒绝与兼容修订恢复；不再从用户过滤后的接口响应构造基线。这些是测试宿主生成的版本制品，不等于正式 NuGet/OCI 发布。CLI 自动产出契约制品、在线签名/发布准入及其它服务接口聚合仍需完成。

## 菜单贡献、资源引用与个人偏好

`NavigationManifestPlugin` 从 `RegistryComponentBinding.FromArtifact` 捕获的 UI 文件读取 `pages[].route / expose / permission / nav`，与实际业务插件共同 Apply，并把菜单登记到 `NavigationItems` 具名集合。它是平台装配器使用的普通包装，不修改 Runtime，也不把清单里的声明自动当成已挂载状态。此包装验证自己消费的菜单字段，不代替完整 MF2 制品、UI 脚本或前端渲染验收。

`Navigation` 服务先读取真实 Registry Ready 视图，再应用 scope 遮蔽、restriction 交集、租户状态和真实 RBAC。业务插件卸载或被租户禁用后，菜单不会从预装清单、个人偏好或被遮蔽的全局项恢复。菜单资源引用采用 `qian:{tenant}:{app}:page:{id}`；本期 tenant 菜单的 app 为组件 ID，id 是规范化编码后的路由，完整引用最多 512 字符。当前提供页面菜单；外壳的页面渲染、外链类型与 App scope 归属仍由相应外壳/应用能力接入。

认证 HTTP 入口：

- `GET /api/capabilities/navigation` 返回当前实际可见的菜单，缓存声明为 private；隐藏偏好在权限过滤之后应用。
- `GET /api/capabilities/navigation/item?resourceRef=...` 解析当前有权限的资源，未知、无权限与未挂载统一 404。个人隐藏是展示偏好，不影响此访问判定。
- `PUT /api/capabilities/navigation/preference?resourceRef=...` 只接受 `{ hidden, order? }`，修改当前主体的偏好。租户、主体类型和主体 ID 来自已验证的端口，不能从请求体指定。

偏好保存在 `CapabilitiesDbContext` 的 `plugin_qian_capabilities.navigation_preferences`，由宿主按 Data 约定注册连接和迁移。表强制租户 RLS，服务查询另按 `principal_kind + sub` 过滤；这也让同名用户与服务拥有独立空间。偏好行不会授予权限，另一租户的资源引用不能写入。

偏好更新与 `qian.capabilities/navigation.preference` 审计在同一个 PostgreSQL Serializable 事务中提交。审计只写已知非敏感输入的规范化哈希，字段为 hidden、order 和 ASCII 资源引用，payload 不复制输入。审计失败时偏好回滚；序列化、死锁及目标键冲突会重新验证资格并重试整笔更新，耗尽后返回 CONFLICT。宿主需给本上下文的受限应用角色配置既有审计事务函数权限，不授予审计历史 DML。

真实 Kestrel/PostgreSQL 用例覆盖两个租户、两个服务主体的展示隔离，版本化租户菜单替换、禁用不回退、restriction、卸载、偏好恢复、审计失败回滚和并发更新单行。参考宿主用 UI 声明验证后端目录；没有把它当成前端远程模块已部署或真实用户交互登录的证据。

## proto 与实际 gRPC 服务探测

`IProtoCompatibilityProbe` 由 `BufProtoCompatibilityProbe` 实现，使用锁定版本 [Buf 1.73.0](https://github.com/bufbuild/buf/releases/tag/v1.73.0)。同一安装脚本按 `proto-tools.lock.json` 校验原始二进制 SHA-256。运行环境用 `QIAN_BUF_PATH` 指向工具；CI 实际安装并执行，工具缺失不会跳过测试。

`ProtoContractSet` 只保存同一制品中的 proto 文件，复制字节并沿用制品路径、文件数和总大小限制。编译使用独立目录、固定配置、禁止符号链接、清空继承环境及受控缓存，不读取制品中的 Buf 配置、网络依赖或生成器。输出包含依赖的标准 FileDescriptorSet。版本升级按 FILE 规则检测源码、JSON 和 wire 破坏性变化；语法错误、缺少导入、检查异常和超时均关闭准入。

`GrpcReflectionContractProbe` 使用宿主提供的 CallInvoker 和认证元数据连接真实 HTTP/2 reflection 服务，复制凭据且不写入诊断。它查询声明中的每个服务，收集文件和依赖，核验冲突、大小及 RPC 最终状态，再按 PACKAGE 规则检查 RPC、消息与字段；允许服务构建的 ProtoRoot 文件前缀不同，不因仅有文件布局差异误拒。两类规则依据 [Buf 兼容规则说明](https://buf.build/docs/breaking/rules/)。宿主负责端点、TLS、出网范围和探测授权；探测入口不接受客户端 URL 或可信标记。

`GrpcContractPlugin` 在业务 Apply 前先检查精确版本制品，再探测实际服务；任一失败都不能公开业务注册。测试使用实际 protobuf Echo 调用、官方 reflection 实现和 RS256 认证，验证正确协议、类型漂移、匿名拒绝、挂载失败与认证恢复后的真实声明式表面登记。此证明不代替完整注册表/生命周期安装编排或生产服务部署验收。
