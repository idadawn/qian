# 租户运营页面

这是 qian.tenancy 的 ui 表面源码，作为私有 pnpm 工作区包交付。页面使用 @yue/form 的 SchemaForm、@yue/data 的 DataTable 与 @yue/sdk 的 PlatformClient，表单校验和 TypeScript 类型均从 [运营 HTTP 契约](../../plugins/qian.tenancy/contracts/http-v1.openapi.json) 派生。后端契约由 TEN-011 统一收口，页面只消费；源码可用于 platform 的运营入口，MF2、ui/manifest.json 与 qian add 的 UI 分发仍由 YUE-001 交付，此处不创建第二份清单。

## 本机运行

先按 [真实运营宿主说明](../../tools/Qian.Dev.TenantOps/README.md) 运行 `scripts/dev-tenant-ops.sh up`，再运行 `pnpm --filter @qian/tenancy-ui dev`，打开 <http://127.0.0.1:5177/>。默认代理指向本机 5080；自定义宿主时给前后端相同的 QIAN_DEV_STATE，并用 QIAN_DEV_OPS_URL 指定已运行的本机宿主。

开发代理只监听回环地址，复用宿主本地密钥签发平台测试身份；Bearer 仅在 Vite 服务端内存中，不进入浏览器、localStorage、sessionStorage、URL、iframe 或构建产物。代理默认身份是平台超管，所以 vite.config.ts 按最终解析出的监听地址把关：不是 127.0.0.1、localhost 或 ::1 就拒绝启动，命令行 `--host 0.0.0.0` 也会被拦。`vite preview` 继承同一份 `server.proxy`，同样以平台超管身份转发，同样只允许回环地址。e2e 入口在运行 Playwright 之前先自测这道守卫。代理仅是本机验收接线；正式外壳须注入已配置认证的 PlatformClient，登录、刷新和会话选择属于 IDP-003/YUE-002。创建表单当前按契约填写管理员 membershipId/subjectId，Identity 成员与管理员登录尚未在本页交付。

## 行为与验证

页面包括服务端筛选分页、新建/编辑、详情/配额、由状态机元数据给出的激活/冻结/恢复操作。编辑携带 expectedRevision；409 在抽屉内提示刷新，不覆盖他人更新。抽屉里的错误只在抽屉内提示一处，不再出现页面级提示：字段错误按 pointer 定位到字段，其他 ErrorDetail（如 403）显示服务端原文；平台 `CONFLICT` 按场景显示租户号重复或编辑冲突文案，其他 409（如 `TENANCY_STATE_CONFLICT`）显示服务端原文；连接失败显示「暂时无法连接服务」。页面级错误提示只来自行内的冻结、恢复。用量接口 usageAvailable=false 显示用量不可用；401/403 单独显示身份状态。网络失败可重试，字段错误使用真实 ErrorDetail pointer 定位。

`pnpm --filter @qian/tenancy-ui e2e` 创建独立临时状态目录、随机回环端口和自有 PostgreSQL 容器，复用 up 启动真实 Kestrel/RS256/受限数据库与平台授权链，再执行 Playwright 七步验收，最后关闭自有宿主和容器。验收另外记录列表请求的完整查询串，断言翻页、每页条数和筛选都以 `skip` / `take` / `keyword` / `status` 交给服务端执行，而不是在前端过滤。无替身响应；非法字段测试仅改出站请求以验证真实 400。产物在 TestResults/tenant-ui，HTTP 证据仅含方法、路径、状态码，不记录令牌。CI 使用同一入口。

修改服务端契约后运行 `pnpm --filter @qian/tenancy-ui contracts:generate`；contracts:check 同时校验类型和运行时 JSON 副本的新鲜度。分层、lint、类型、循环依赖和构建均接入 yue 门禁；本页真实宿主 E2E 独立于靶场。
