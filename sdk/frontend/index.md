> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# 前端 SDK

前端外壳自身也是一棵由 `@qian/runtime` 装载的插件树，与后端同一套语义：上下文、服务注册、类型化事件、可逆副作用、scope。

| 包                  | 内容                                                 |
| ------------------ | -------------------------------------------------- |
| `@qian/runtime`    | 运行时：上下文 `ctx`、`provide` / `inject`、事件、副作用、装载器      |
| `@qian/plugin-sdk` | 插件开发包的前端部分：与外壳交互的桥接 API（随 qian.shell 提供，PRD §7.15） |
| `@yue/sdk`         | 注册表 API、`PlatformClient`、请求拦截器链（鉴权刷新、租户头）、i18n     |

- **服务键**：TS 侧小驼峰（`files`），.NET 侧大驼峰（`Files`），两侧各自声明常量，文档生成器把它们对应到同一 seam。`ctx.files` 是代理属性，等价于 `ctx.inject("files")`。
- **远程模块**：外壳 [qian.shell](/components/platform/qian.shell.md) 是 MF2 宿主，按需加载插件的远程模块；`react`、`react-dom`、`@yue/*`、`@qian/plugin-sdk` 作为共享单例。
- **插槽**：导航、设置、用户菜单、命令、预览等插槽都带版本号与输入数据 Schema。

## API 草图

节选自[组件模型规格](/design/prd/specs/component-model.md) §5.3（以规格为准）：

```ts
export type ScopeKey = "global" | `${"tenant" | "app" | "agent-session"}:${string}`;
export type DispatchMode = "emit" | "waterfall" | "parallel" | "serial" | "bail";

export interface QianPlugin<C = unknown> {
  readonly name: string; // 组件 id
  readonly inject?: readonly ServiceKey[];
  apply(ctx: Context, config: C): void | Promise<void>;
}

export interface Context {
  readonly scope: ScopeKey;
  provide<T>(key: ServiceKey<T>, value: T): Disposer;
  inject<T>(key: ServiceKey<T>): T;
  on<P, R>(evt: QianEvent<P, R>, mode: DispatchMode, handler: HandlerFor<P, R>, priority?: number): Disposer;
  dispatch<P, R>(evt: QianEvent<P, R>, payload: P): Promise<R>;
  scoped(scope: ScopeKey): Context;
}
```

组件的界面怎么写、组件怎么演示，见 [yue 组件库](/components/yue/index.md)；API 参考（TypeDoc 生成）随 S1-DOCS-002 上线。
