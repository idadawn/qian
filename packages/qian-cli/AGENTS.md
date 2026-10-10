# CLI 约定

- 当前提供静态 dump-config、docs generate 与 skill-only 文档模板初始化；文件读取和输出不得执行目标组件或读取密钥。
- 复用 @qian/surfaces，规则改动必须更新 .NET 装配器与共同 TCK。
- 参数错误也输出统一 ErrorDetail，不拼 { error: ... }。
