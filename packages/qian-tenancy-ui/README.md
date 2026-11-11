# 租户运营页面

这是 qian.tenancy 的 ui 表面源码，作为私有 pnpm 工作区包交付。页面使用 @yue/form 的 SchemaForm、@yue/data 的 DataTable 与 @yue/sdk 的 PlatformClient，表单校验和 TypeScript 类型均从 [运营 HTTP 契约](../../plugins/qian.tenancy/contracts/http-v1.openapi.json) 派生。后端契约由 TEN-011 统一收口，页面只消费；源码可用于 platform 的运营入口，MF2、ui/manifest.json 与 qian add 的 UI 分发仍由 YUE-001 交付，此处不创建第二份清单。

## 本机运行

先按 [真实运营宿主说明](../../tools/Qian.Dev.TenantOps/README.md) 运行 `scripts/dev-tenant-ops.sh up`，再运行 `pnpm --filter @qian/tenancy-ui dev`，打开 <http://127.0.0.1:5177/>。默认代理指向本机 5080；宿主同时在 5081 起真实 qian.identity，管理员的账号、成员与密码都经它。自定义宿主时给前后端相同的 QIAN_DEV_STATE，并用 QIAN_DEV_OPS_URL 指定已运行的本机宿主。

开发代理只监听回环地址，复用宿主本地密钥签发平台测试身份；Bearer 仅在 Vite 服务端内存中，不进入浏览器、localStorage、sessionStorage、URL、iframe 或构建产物。代理默认身份是平台超管，所以 vite.config.ts 按最终解析出的监听地址把关：不是 127.0.0.1、localhost 或 ::1 就拒绝启动，命令行 `--host 0.0.0.0` 也会被拦。`vite preview` 继承同一份 `server.proxy`，同样以平台超管身份转发，同样只允许回环地址。e2e 入口在运行 Playwright 之前先自测这道守卫。代理仅是本机验收接线；正式外壳须注入已配置认证的 PlatformClient，登录、刷新和会话选择属于 IDP-003/YUE-002。

## 行为与验证

页面包括服务端筛选分页、新建/编辑、详情/配额、由状态机元数据给出的激活/冻结/恢复操作，以及管理员维护（S1-TEN-010）：

- 新建租户先选定管理员：按登录标识查找已有账号，或新建账号（一个登录标识加初始密码，首次登录强制修改），再填主档。成员 ID 在选定管理员时生成一次，同一份开户请求重复提交保持幂等。
- 详情显示当前管理员的掩码标识，可更换管理员、重置密码。两者都按状态机元数据的 `requiresConfirmation` 二次确认；元数据缺席时同样确认。
- 账号查找、新建与重置的表单都从契约派生；初始密码与临时密码按密码框渲染（`autoComplete=new-password`），只读时整体打码。

列表按开通流程原型改版（S1-TEN-018），S1-YUE-019 再按原型逐块复刻：

- 状态页签（全部、正常、试用、待激活、宽限期、已冻结）替代状态下拉框。页签计数取自列表接口的 `counts`，只受关键字影响、不受所选状态影响；切换页签以 `status` 交给宿主过滤。
- 工具行：搜索框（占位「搜索租户名称或租户号」）、「重置」清空关键字；右端是表格自带的「显示列」与刷新按钮。
- 列为租户（方形标识色块按租户号稳定取色、已冻结为灰色；名称点开详情，下方等宽租户号）、状态、有效期、用户（已用 / 上限加 4px 进度条）、管理员、创建时间（只显示日期）与操作。列宽取原型画板里各列的实际宽度、等比缩到合计 900px 作为最小宽，表格更宽时按比例分配（原型宽度下与画板一致）；名称与租户号最宽 200px、管理员最宽 120px，更长时省略。
  - 有效期：待激活且没有截止时间显示「—」（读屏读「未激活，尚无有效期」）；其余没有截止时间显示「长期」；宽限期标出原截止日；宽限期与七天内到期用警示色。
  - 用户用量来自身份组件的成员数，九成以上进度条用警示实色。身份组件不在时显示「—」并带读屏说明，不报 0。
- 行内只留「详情」「编辑」，其余收进「更多」菜单：状态机给出的合法迁移（激活、恢复；冻结是破坏性操作，放在分隔线后）与管理员维护（更换或设置管理员、重置管理员密码）。列表行没有成员 ID，重置管理员密码先读该租户的管理员：只有一位时直接给重置表单，多位时逐个列出。菜单项执行前把焦点交回「更多」，随后的抽屉或确认框关闭时焦点回到这一行。
- 详情抽屉分「基本信息」「配额与用量」「管理员」三段，页头带状态徽标。
- 抽屉里的查找账号、新建账号是中间步骤，按钮留在表单内；页脚只放主表单的取消与提交。
- 页面接收外壳的 `locale`（remote 入口传入），简体中文、繁体中文与英文文案齐全。
编辑携带 expectedRevision；409 在抽屉内提示刷新，不覆盖他人更新。抽屉里的错误只在抽屉内提示一处，不再出现页面级提示：字段错误按 pointer 定位到字段，其他 ErrorDetail（如 403）显示服务端原文；平台 `CONFLICT` 按场景显示租户号重复或编辑冲突文案，其他 409（如 `TENANCY_STATE_CONFLICT`）显示服务端原文；连接失败显示「暂时无法连接服务」。页面级错误提示只来自行内的冻结、恢复。用量接口 usageAvailable=false 显示用量不可用；401/403 单独显示身份状态。网络失败可重试，字段错误使用真实 ErrorDetail pointer 定位。

`pnpm --filter @qian/tenancy-ui e2e` 创建独立临时状态目录、随机回环端口和自有 PostgreSQL 容器，复用 up 启动真实 Kestrel/RS256/受限数据库与平台授权链，再执行 Playwright 七步验收与管理员维护验收，最后关闭自有宿主和容器。管理员维护部分：租户 A 在开户抽屉里新建管理员账号，租户 B 按登录标识选择已有账号；两位管理员经身份组件的账号接口（统一登录流水线）登录、首次登录先改初始密码，`/api/identity/me` 与目录成员列表都只有本租户；页面重置 A 的管理员密码（详情抽屉）与更换 B 的管理员（行内「更多」菜单）都要二次确认，重置后旧密码与原会话立即失效、临时密码登录必须先改密；激活、冻结、恢复同样经「更多」菜单，菜单只列当前状态的合法迁移。验收另外记录列表请求的完整查询串，断言翻页、每页条数和筛选都以 `skip` / `take` / `keyword` / `status` 交给服务端执行，而不是在前端过滤。无替身响应；非法字段测试仅改出站请求以验证真实 400。产物在 TestResults/tenant-ui，HTTP 证据仅含方法、路径、状态码，不记录令牌。CI 使用同一入口。

修改服务端契约后运行 `pnpm --filter @qian/tenancy-ui contracts:generate`；contracts:check 同时校验类型和运行时 JSON 副本的新鲜度。分层、lint、类型、循环依赖和构建均接入 yue 门禁；本页真实宿主 E2E 独立于靶场。

## 官方 MF2 远程（S1-YUE-001）

`pnpm --filter @qian/tenancy-ui build:remote` 使用独立 `vite.remote.config.ts`，输出 `dist/remote/mf-manifest.json`、`remoteEntry.js` 与真实类型归档。`TenantPage` expose 复用本目录同一页面和后端生成契约，接收宿主的 PlatformClient 与受 scope 约束的 bridge；React、yue 和插件 SDK 仅消费宿主 singleton，不包含开发身份代理。清单在 [qian.tenancy/ui/manifest.json](../../plugins/qian.tenancy/ui/manifest.json)。原 `dev / build / e2e` 本机运营手测入口保持不变。

外壳按需加载、租户未挂载零请求、共享实例、预加载与卸载由 [qian.shell 浏览器验收](../../apps/qian-shell/README.md) 覆盖；S1-TEN-005 原运营 API 测试仍单独执行，不能由 MF 页面加载冒充。
