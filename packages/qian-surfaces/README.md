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

返回 ErrorDetail 外加文件、行号和级别。调用方可按 pointer 修复源文件；跨组件引用在不完整目录下告警，完整目录下拒绝未知项；给出 `serviceKeys` 时 `inject[]` 引用目录外的服务键为 error。任何文件内容都不会被执行。

## 已知限制与延期项

与 [.NET Provider](../Qian.Surfaces/README.md) 一致：不包含 Agent、shell、MCP 或 hook 执行器，也不实现文件监视或完整安装事务。YAML 采用无 alias、单文档、无指令的安全子集，单文件 1 MiB、容器最多 64 层；字符串与键名不能含未成对的代理项，块映射里的空键等两库处理不同的写法两端都拒绝。诊断的先后、行号与文案同 .NET Provider，见其[检查顺序与行号约定](../Qian.Surfaces/README.md#检查顺序与行号约定)。API Extractor 当前捆绑 TypeScript 5.9，源码另以工作区 TypeScript 6 编译；公开接口只使用两者共同支持的语法。

## 表面运行态

本包提供结构化 SurfaceContext 端口、SurfaceRegistrationSession 与 HotReloadSurfacePlugin，可直接消费实际 Qian TypeScript 运行时上下文。端口要求 provide 返回带 replace 的注册句柄（与运行时的 RegistrationHandle 同构），会话据此原位替换内容改变的表面。具体语义与边界统一维护在 [.NET Provider 主文档](../Qian.Surfaces/README.md#声明式表面独立刷新)，两侧共用 T19 文件监视场景。运行热重载用例前需构建 packages/qian-runtime；CI 已按此顺序执行。
