# SCIM 2.0 核心 Schema 契约 v2

S1-IDP-010 冻结 Qian 采用的 SCIM 2.0 核心 Schema（RFC 7643）：User、Group 与企业用户扩展。SCIM 服务本身（`/scim/v2` 下的 Users、Groups、Schemas、ResourceTypes 端点，过滤、PATCH 与批量，RFC 7644）由 S3 实现；本目录只定 Schema 和 Qian 目录到 SCIM 的映射口径，目前没有端点读写这些文件。

## 文件

| 文件 | 内容 |
|---|---|
| [user.scim-schema.json](user.scim-schema.json)、[group.scim-schema.json](group.scim-schema.json)、[enterprise-user.scim-schema.json](enterprise-user.scim-schema.json) | RFC 7643 第 7 节格式的 Schema 资源表示，即将来 `/Schemas` 端点返回的形状。每个属性给出类型、是否多值、是否必填、大小写敏感、可变性、返回方式与唯一性；属性与特征按 RFC 7643 第 4 节，描述用中文自写 |
| [resource-types.json](resource-types.json) | 两个 ResourceType 表示：User（`/Users`，企业扩展可选）与 Group（`/Groups`） |
| [user.schema.json](user.schema.json)、[group.schema.json](group.schema.json) | 资源实例的 JSON Schema（2020-12），由上面的表示推导：只认规范大小写的属性名，未声明的属性一律拒绝，带企业扩展时 `schemas` 必须列出扩展的 URN |
| [fixtures/user.qian.json](fixtures/user.qian.json)、[fixtures/group.qian.json](fixtures/group.qian.json) | 按下文映射从租户目录推出的样例，id 与 [目录查询契约](../../identity/v1/directory.openapi.json) 的示例是同一组 |
| [fixtures/user.full.json](fixtures/user.full.json) | 每个属性都填上的 User 样例 |

## 与 RFC 7643 的取舍

- Group 的 `displayName` 按第 4.2 节正文定为必填（第 8.7.1 节的示例表示里写成了非必填）。
- `Group.members` 带 `display` 子属性（第 2.4 节列出的多值属性缺省子属性），只读，由服务提供方按成员的显示名填写。
- 属性名在协议上不区分大小写（第 2.1 节）。JSON Schema 只认规范写法，S3 实现入站解析时要按不区分大小写处理。
- 只支持企业用户扩展；`schemas` 里出现其他扩展 URN 即不符合本契约。
- `canonicalValues` 只是建议取值，JSON Schema 不把它写成枚举。

## Qian 目录 → SCIM 映射

User：一个租户目录成员对应一个 User；同一自然人加入两个租户就是两个 User。

| SCIM 属性 | Qian 来源 | 说明 |
|---|---|---|
| `id` | `Membership.id`（目录成员 id） | 不用 `person_id`：它在各租户相同 |
| `userName` | `Membership.account` | 租户内唯一、不可改 |
| `displayName` | `Membership.display_name` | |
| `name.formatted` | `UserProfile.name` | 没有档案时省略 |
| `active` | `Membership.status` | `active` 为 true；`disabled`、`locked` 为 false |
| `emails`（`type` 为 `work`） | `UserProfile.email` | 档案里的联系方式，不是登录标识 |
| `phoneNumbers`（`type` 为 `work`） | `UserProfile.phone` | 同上；规范形式是 `tel:` URI |
| `groups` | 成员的用户组归属（`MemberRelation` 中 `target_type` 为 `group`） | `type` 固定为 `direct`：Qian 的用户组不嵌套 |
| 企业扩展 `department` | 主组织（`organization` 类归属中 `is_primary` 的那条）的 `Organization.name` | 没有主组织时省略 |
| 企业扩展 `manager.value`、`manager.displayName` | 目录直属主管查询 `GET /api/directory/members/{id}/manager` 的 `manager.id`、`manager.display_name` | 口径与目录的直属主管相同；没有主管时省略 |
| `meta.resourceType` | 固定为 `User` | |

Group：

| SCIM 属性 | Qian 来源 | 说明 |
|---|---|---|
| `id` | `DirectoryGroup.id` | |
| `displayName` | `DirectoryGroup.name` | |
| `members[].value`、`members[].display` | 组内成员的 `Membership.id`、`Membership.display_name` | `type` 固定为 `User` |
| `meta.resourceType` | 固定为 `Group` | |

留给 S3 决定：`externalId` 的存放、是否经 SCIM 接受 `password`、SCIM 客户端能否读取联系方式明文、从目录删除的成员如何呈现、`meta` 的版本与时间口径、端点地址，以及 `title`、`nickName`、`employeeNumber`、`costCenter`、`organization`、`division` 等未映射属性。档案里的拼音没有对应的 SCIM 属性。

## 测试

`Qian.Identity.Abstractions.Tests` 的 `ScimContractsTests` 校验：三份表示的 Schema URN、属性集合与属性特征（RFC 7643 第 7 节的取值范围）；JSON Schema 与表示逐属性、逐子属性一致（类型、多值、必填、只读 / 只写）；ResourceType 引用的 Schema 都在本目录；样例通过 JSON Schema，缺必填、未声明属性、扩展未在 `schemas` 声明等反例被拒。
