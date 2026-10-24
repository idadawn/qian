---
"@idadawn/yue-form": minor
---

SchemaForm 的数据 Schema 与 FormSchema / ListSchema / WidgetMeta 元数据 Schema 改为解释型校验，不在运行期生成代码（外壳 CSP 不放行 `'unsafe-eval'` 也能用），不再依赖 ajv / ajv-formats。只接受 SchemaForm 用到的 JSON Schema 2020-12 子集，子集内的判定、错误顺序与错误对象与此前的 Ajv 2020（allErrors、strict: false、ajv-formats full）逐条一致；子集外的关键字、格式与引用在渲染前由 `assertFormSchema` 拒绝（`form.error.unsupportedKeyword`），参数不合法拒绝（`form.error.schema`），此前它们会被 Ajv 照常校验或忽略，属于收窄。`validateValues` / `validateMetadata` 的错误类型改为本包导出的 `SchemaError`（与 Ajv `ErrorObject` 同形），声明文件不再引用 ajv。

发版注意：`@idadawn/yue-*` 是固定版本组，这条 minor 会把整组带到 0.2.0。外壳的 `__YUE_VERSION__` 取自 yue-shell 版本，插件 SDK 宿主按清单的 `engines.yue` 做 SemVer 检查（`packages/qian-plugin-sdk/src/host.ts` 的 `#version`），`^0.1.0` 不接受 0.2.0，远程会被拒载。同一次发版里要同步：`plugins/qian.tenancy/ui/manifest.json` 与 `samples/qian-sample-crm-ui/ui/manifest.json` 的 `engines.yue`，以及 `packages/qian-tenancy-ui/vite.remote.config.ts` 与 `samples/qian-sample-crm-ui/vite.remote.config.ts` 里 yue 共享依赖的 `requiredVersion`（插件 SDK 另有一条 minor，`.changeset/shell-fusion-subpaths.md`，同一处 `@idadawn/qian-plugin-sdk` 的 `requiredVersion` 也要改）。仓库外的远程同样要放宽 `engines.yue` 后重发。
