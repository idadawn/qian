> For AI agents: the complete documentation index is available at /pr-preview/pr-311/llms.txt, the full documentation bundle is available at /pr-preview/pr-311/llms-full.txt.

# 业务选择器

`@idadawn/yue-biz` 是需要知道「平台」存在的选择器层（[yue 规格](/pr-preview/pr-311/design/prd/specs/yue.md) §3.1）。所有组件都只经宿主注入的 `PlatformClient` 取数，组件里没有第二条网络路径；文案全部走 i18n key，宿主用 `createBizI18n()` 新建实例，或对已有实例调用 `addBizLanguagePacks(i18n)`（会一并装入 data 的语言包）。组织选择与授权树建在 `@idadawn/yue-data` 的 Tree / TreeSelect 上，不另做一套树（规格 D3）。

以下示例与靶场共用源码，只替换 SDK 网络层：mock 中间件按 PRD 7.4.3 的目录 API 与 PRD 7.5 的角色 / 权限点目录返回数据，接真实服务时组件代码不变。

## 取数契约

路径、参数与响应类型由 `packages/yue/biz/contracts/biz.openapi.json` 生成（`pnpm --filter @idadawn/yue-biz contracts:generate`，CI 比对漂移）。

| 接口 | 来源 | 用途 |
| ------------------------------------------------------------------- | --------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| `GET /api/directory/orgs/tree` | 身份组件目录 API（已交付） | OrganizeSelect：按 path 排序的扁平列表，以 parent_id 组树 |
| `GET /api/directory/members`、`GET /api/directory/orgs/{id}/members` | 身份组件目录 API（已交付） | UserSelect：`q` 服务端检索（显示名、账号、拼音），`ids` / `person_ids` 按 id 批量回显已选值（不分页，不带 offset / limit）；检索按 offset / limit 分页，limit 不超过 200 |
| `GET /api/directory/org-types` | 身份组件目录 API（已交付） | 内置字典 orgTypeDictionary |
| `GET /api/authz/roles` | PRD 7.5 角色目录草案 | RoleSelect |
| `GET /api/authz/permissions` | PRD 7.5 权限点目录草案 | 授权树 |

目录接口与身份组件逐字段一致（snake_case，空值字段省略）：身份组件的单测把这里的目录部分与 `contracts/identity/v1/directory.openapi.json` 逐字段比对，真实目录的 E2E 见靶场的真实目录页（S1-IDP-010）。角色与权限点目录尚无服务端实现：草案字段取自 PRD 7.5 的 roles 与 permission_definitions 表，线上形状沿用 `GET /api/authz/me/permissions` 的 camelCase，路径与字段由 S1-AZ-005 接线时确认。

## 取值口径

| 组件 | 选中值 | 说明 |
| -------------- | ------------- | -------------------------------------------------------------------------------------------------------------- |
| OrganizeSelect | 组织 id | 单选 `string \| null`，`multiple` 时为 `string[]` |
| UserSelect | 成员 id 或自然人 id | `valueKey="membership"`（默认）给目录成员 id，供成员归属等目录 API；`valueKey="person"` 给自然人 id（令牌 sub），授权侧 user 主体的 subjectId 用它 |
| RoleSelect | 角色 code | 与授予端口 ITenantRoleGrant 的 roleCode 一致 |
| 授权树 | 权限点全名数组 | `<plugin-id>:<resource>:<action>`；分组节点不进入值，目录外的已授予项原样保留 |
| DictSelect | 字典项 value | 组织类型字典的 value 是类型编码 |
| ApiSelect | getValue 的返回值 | 由调用方决定 |
| IconPicker | 图标登记名 | 即 UI 清单 nav.icon 的取值 |

选择器共有的属性：`client`（IconPicker 除外）、`i18n`、`label`（触发器与弹层的可访问名称，可见标签由表单 Field 或宿主提供）、`value` / `onChange`、`multiple`（OrganizeSelect、UserSelect、RoleSelect、DictSelect、ApiSelect）、`disabled`、`className`、`testId`。加载失败时触发器旁出现错误与可键盘到达的重试按钮；已选值显示清空按钮。

## ApiSelect 与 useRemoteOptions

远程选项选择器：`load(client, { keyword, signal })` 只能经传入的 client 取数，`getValue` / `getLabel` 把数据项映射成选项，可选 `getDescription`、`isItemDisabled`。`search="server"`（默认）时输入去抖（`debounceMs`，默认 250）后作为 keyword 重新加载，只采纳最新一次结果，已选项在搜索后仍显示名称；`search="client"` 只加载一次并在本地过滤；`reloadKey` 变化即重新加载。


本页交互预览支持状态、主题切换与本地代码编辑。示例源码见下方「用法与源码」。


`useRemoteOptions({ client, load, keyword, debounceMs, enabled, reloadKey })` 是同一取数核心，返回 `items`、`seen`、`loading`、`error`、`reload`，可直接用于自定义选择器；内联的 load 函数不会造成重复请求。

## DictSelect

字典选择器：`dictionary` 是 `defineDictionary(id, load)` 定义的字典，字典项为 value、label 或 labelKey（内置字典走 i18n）、disabled。字典项按 PlatformClient 实例与字典 id 全量缓存，多个选择器共享一次在途请求，失败不进缓存；字典在别处修改后调用 `invalidateDictionary(client, id)`。内置 `orgTypeDictionary` 读租户组织类型。


本页交互预览支持状态、主题切换与本地代码编辑。示例源码见下方「用法与源码」。


## OrganizeSelect

组织选择器：单选直接复用 data 的 TreeSelect；`multiple` 时在同一 Tree 上做不级联的复选（`cascade={false}`），选上级不会带上下级，适合成员的多组织归属。`buildOrganizationTree(organizations)` 把目录接口的扁平列表组装成 Tree 节点，页面的左侧组织树也可以直接用它。


本页交互预览支持状态、主题切换与本地代码编辑。示例源码见下方「用法与源码」。


## UserSelect

用户选择器：输入去抖（`debounceMs`，默认 250）后交给目录接口按显示名、账号与拼音检索，`organizationId` 限定组织，`descendants` 默认含子树；停用、锁定的成员带状态说明。每次显示第一页 50 位，后面还有时在触发器旁提示继续输入。已选值不在检索过的结果里时（初始值、换了组织范围）按 id 批量回查显示名，`valueKey="person"` 时按自然人 id 回查；查不到的值显示原值，不重复请求。


本页交互预览支持状态、主题切换与本地代码编辑。示例源码见下方「用法与源码」。


## RoleSelect

角色选择器：内置角色（tenant.owner / tenant.admin / tenant.developer / tenant.user）按 code 取文案 `biz.role.<code>`，自定义角色显示服务端名称，缺省为 code；选项右侧标出「内置」或 code。


本页交互预览支持状态、主题切换与本地代码编辑。示例源码见下方「用法与源码」。


## 菜单 / 按钮授权树

AuthorizationTree 接收 `client`、`i18n`、`label`、`value`（已授予的权限点全名）、`onChange`。口径：一个资源类型是一个菜单（UI 清单的页面以该资源的 read 为门槛权限），菜单下的按钮是该资源的各个动作，read 排第一表示「菜单可见」。勾选任一按钮自动带上同菜单的 read，取消 read 同时取消该菜单的其余按钮；勾选菜单授予其全部权限点。按钮文案按动作取 `biz.action.<action>`，没有登记的动作显示动作名；高风险、关键操作与需二次验证的权限点在名称后标出。数据口径可单独复用：`groupPermissionCatalog`、`buildAuthorizationTree`、`linkAuthorization`。


本页交互预览支持状态、主题切换与本地代码编辑。示例源码见下方「用法与源码」。


前端勾选只是体验层：授权以服务端为准，真实授予与生效的 E2E 在 S1-AZ-005。

## IconPicker

图标选择器：从 `@idadawn/yue-core` 的统一 Icon 登记表（`ICON_NAMES`）里选，按图标名过滤，触发器显示所选图标；`icons` 可限定可选范围。图标集是本地登记表，不需要取数，因此没有 client 属性。


本页交互预览支持状态、主题切换与本地代码编辑。示例源码见下方「用法与源码」。


## 限制

岗位、用户组选择器，弹窗式多选（UsersSelect、PopupSelect 族）与授权树的列 / 字段 / 数据页签留给 S2。UserSelect 只显示检索结果的第一页，不做滚动加载。角色与权限点目录是草案，服务端由 S1-AZ-005 交付。所有示例与浏览器测试共用源码，场景登记在 biz/docs/scenarios.json。
