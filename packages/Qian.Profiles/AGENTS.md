# profile 装配约定

- 权威规则为组件规格 §7；默认 minimal 以 S1-DEC-012 裁决为准，不自行消解设计冲突。
- 两端共用 tck/profiles，输出必须覆盖来源、配置、顺序与诊断的一致性。
- config 整段替换；不深合并旧值，不联网解析 Schema 或密钥。
- 仅依赖 Definition 与 Runtime，表面解析从 ISurfaceReader 传入。
- 不把 x-skeleton 清单当成已启动的 Provider，不在装配阶段执行组件代码。
