# OpenAPI 类型生成工具

此私有工具为前端消费方从组件 OpenAPI 生成 paths 与模型类型，HTTP 请求仍由 yue SDK 的 PlatformClient 执行。

根目录运行 `pnpm openapi:generate --source <契约.json> --output <生成.ts>`；加 `--check` 只比对，不改文件。各消费包提供 contracts:generate / contracts:check 别名，CI 检查契约和类型是否漂移。

openapi-typescript 7.13 的 peer 仅支持 TypeScript 5，因此工具单独通过命名 catalog 使用 5.9.3，和生成器一起锁在 pnpm-lock.yaml。产品代码仍按工作区 TypeScript 6 编译；不放宽 peer 范围，不覆盖或降低全局 TypeScript 版本。
