# 配置 Schema 与配置值 HTTP 表面

- 主文档为 ../Qian.Config.Apollo/README.md 的「配置 Schema 与配置值接口」一节，按 S1-CFG-003 交付。
- 只依赖 Definition 与 Runtime：Schema 取自 Registry 预装集合中由 FromArtifact 捕获的制品文件，配置值取自 IConfigService 的 qian.plugin.<组件 id> 节，判定交给 IAuthzEnforcement；不依赖 Provider、Kun.Security 或 Qian.Authz.AspNetCore。
- Schema 不含配置值，对已认证调用方可读；配置值只对平台主体开放，执行端口放行后才读，敏感键（Schema 标 x-qian-sensitive、疑似密钥键名或值）只给键名，不给值也不给引用。
- 权限点在处理器内判定一次，端点只带 RequiresPermission 元数据供 OpenAPI 写出 x-qian-permission；宿主不要再给本端点加 RequireQianPermission，否则重复判定、重复记决策审计。
- 先判定再查组件是否存在，未授权调用方看不到哪些组件已安装；错误统一 ErrorDetail，响应带 Cache-Control: private, no-store，公共契约同步 PublicAPI。
