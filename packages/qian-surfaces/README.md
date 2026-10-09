# @qian/surfaces

TypeScript 声明式组件校验库。复用 [共同 Schema](../../schemas/v2/README.md) 与 [语言无关 fixture](../../tck/surfaces/README.md)，与 .NET Provider 对齐；本包通过 `pnpm --filter @qian/surfaces build` 构建。

```ts
import { validateComponent } from '@qian/surfaces';
const result = validateComponent(componentDirectory, {
  profile: 'minimal', executionPlane: 'human-cli', catalogComplete: true,
  capabilities: ['qian.files/purgeTenantFiles'],
});
```

## 装配位置

适用于 CLI、CI 与静态注册阶段的所有 profile，无平台后端依赖。组件的声明式表面由文件夹分发；`qian add` 安装流程在 CLI 任务中实现。公开声明由 API Extractor 跟踪在 `api/`，Schema 从仓库单点复制到发行目录，不另维护一套。

## 模型体验

返回 ErrorDetail 外加文件、行号和级别。调用方可按 pointer 修复源文件；跨组件引用在不完整目录下告警，完整目录下拒绝未知项。任何文件内容都不会被执行。

## 已知限制与延期项

与 [.NET Provider](../Qian.Surfaces/README.md) 一致：不包含 Agent、shell、MCP 或 hook 执行器，也不实现文件监视或完整安装事务。YAML 采用无 alias 的安全子集，单文件 1 MiB、深度 64。API Extractor 当前捆绑 TypeScript 5.9，源码另以工作区 TypeScript 6 编译；公开接口只使用两者共同支持的语法。
