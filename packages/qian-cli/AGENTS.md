# CLI 约定

- 当前提供静态 dump-config、doctor 的 profile 与实时表面诊断、docs generate、skill-only 文档模板初始化、市场目录解析与无代码组件来源锁定；文件读取和输出不得执行目标组件或解析 secretRef 或读取私钥。
- 复用 @qian/surfaces，规则改动必须更新 .NET 装配器与共同 TCK。
- 参数错误也输出统一 ErrorDetail，不拼 { error: ... }。

- 市场来源必须先验 Ed25519 签名与 digest，再复用组件校验器；不 checkout 或执行来源内容。未验证市场只允许显式选择的 community 无代码组件。
- `add` 的当前切片只安装无代码组件，写 profile patch 和来源 lock；代码依赖安装、升级/移除、租户 scope 与完整恢复入口归 S1-RT-011。禁止用解析成功冒充代码包已安装。
