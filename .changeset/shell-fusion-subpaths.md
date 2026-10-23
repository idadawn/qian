---
"@idadawn/yue-shell": patch
"@idadawn/yue-core": patch
"@idadawn/qian-plugin-sdk": minor
---

yue-shell 增加外壳首屏用的子路径导出 `./router`、`./menu`、`./inbox` 与按需加载的 `./session-dialog`（`SessionExpiredDialog` 仍从根入口导出）；`initPreferences` 新增 `defaults`：部署缺省（如运行时配置给的语言）低于已保存的偏好，且不写进偏好存储。yue-core 的外壳子集 `./shell` 补上 `Badge` 与 `Popover` / `PopoverTrigger` / `PopoverContent` / `PopoverTitle`，站内信弹窗不再把完整组件目录拉进首屏。

qian-plugin-sdk 的 UI 清单与插槽贡献校验改为构建期预编译（由 `schemas/v2/ui.schema.json` 经 Ajv standalone 生成，`scripts/ui-schema.mjs --check` 防漂移）：浏览器运行时不再携带 Ajv 编译器、不在运行期生成代码，`ajv` 改为开发依赖。客户端动作的参数 Schema 改由可解释子集校验（类型、枚举 / 常量、对象、数组、字符串与数值约束及说明关键字）；`$ref`、组合、条件、`format` 等子集外关键字拒绝登记（`INPUT_INVALID`，指针 `/parameters`），此前按完整 2020-12 编译放行。
