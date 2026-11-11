# 身份契约 v1

## 身份事件 payload

五份 `identity.*.v1` Schema 对应 `IdentityEventIds` 和类型上的稳定 `[IntegrationEventId]`，由 `Qian.Identity.Abstractions` 嵌入分发。外层信封沿用 `contracts/v0/event-envelope.schema.json`；UUID 直接引用冻结 S2S Schema 的定义，不再抄写另一份规则。

事件为登录、退出、改密、锁定和会话吊销。只发送身份投影与修订信息，不发送密码、哈希、OTP 或令牌。会话标识是内部失效关联字段，日志只允许哈希或脱敏标识。

## 目录变更事件 payload

十二份 `directory.*.v1` Schema 对应 `DirectoryEventIds`（S1-IDP-010），同样由 `Qian.Identity.Abstractions` 嵌入分发，并随 `RegisterIdentityEvents` 一起登记，嵌入内置账号的宿主不需要另外接线：

| 事件 | 何时发出 |
|---|---|
| `directory.org.created.v1` / `updated.v1` / `moved.v1` / `deleted.v1` | 组织新建；名称、类型、主管或责任岗位变化；换父组织（只为被移动的子树根发一条，带新旧 path）；删除（子树里每个组织各一条，连带删除的岗位各发一条 `directory.position.deleted.v1`） |
| `directory.position.created.v1` / `updated.v1` / `moved.v1` / `deleted.v1` | 岗位的同类变化，规则同组织；被删的岗位若是某组织的责任岗位，该组织另发一条 `directory.org.updated.v1` |
| `directory.member.created.v1` | 成员加入租户 |
| `directory.member.relations_changed.v1` | 成员的组织、岗位、用户组归属集合变化（集合不变不发），同时带替换前后的完整归属（含主归属标记） |
| `directory.member.status_changed.v1` | 成员启用、停用、锁定、从目录删除或重新启用 |
| `directory.member.profile_updated.v1` | 档案内容变化（内容不变不发）；载荷只有成员标识，消费方按需回读 |

载荷只带 id 与结构字段（path、depth、父节点、责任岗位、主管、状态、归属），不带组织名、岗位名、姓名、账号与联系方式；消费方需要名称时经目录查询回读。每条载荷都带 `directory_revision`：本租户目录修订号，每次目录写入加一，同一次写入发出的多条事件共用一个修订号，消费方据此排序、去重。事件与目录写入在同一事务进入 Outbox，写入失败不留事件。

## 目录查询契约（Agent 契约）

[directory.openapi.json](directory.openapi.json) 是目录只读接口的 OpenAPI 3.1 契约：组织类型、组织树、岗位、用户组、成员检索与按 id 批量、部门成员、岗位任职、成员归属、成员档案，以及派生查询——按岗位链的上级责任人、直属主管、部门负责人、我的下属（S1-IDP-010）。每个查询带意图式 description（回答什么问题、什么时候用、什么时候改用别的查询）与请求 / 响应示例，供 Agent 选用查询。

`Qian.Identity.Server.Tests` 的 `DirectoryAgentContractTests` 核对契约登记的路径与 Identity Server 实际的 GET 端点逐一对应、每个查询都有描述与示例、示例符合 Schema、Schema 与目录记录的序列化逐字段一致，以及 yue 业务组件契约（`packages/yue/biz/contracts/biz.openapi.json`）的目录部分与本契约逐字段一致；`DirectoryQueryTests` 用真实会话调遍每个端点与查询参数，响应都要通过契约 Schema 的严格校验。

## 共同约定

载荷不含信封字段（[S1-DEC-020](../../../pm/tasks/S1-DEC-020.md)）：`event_id`、`occurred_at` 由 Qian.Events 写入端入队时生成，`tenant_id` 取发布时的租户上下文，三者只在信封上，消费端从信封读取。生产方在事件所属租户的作用域内入队；平台级身份事件在租户作用域外入队，信封 `tenant_id` 为 null。目录事件一律在所属租户作用域内入队。

字段、属性标记和源生成 JSON 由 `Qian.Identity.Abstractions.Tests` 逐一校验。自首个发布版本起，破坏性变化新增 v2 类型与 Schema，不覆盖 v1；发布前去掉信封字段这一次就地修正由 S1-DEC-020 裁决。

SCIM 2.0 核心 Schema 不在本目录，见 [contracts/scim/v2](../../scim/v2/README.md)。
