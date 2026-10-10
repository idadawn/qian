# Qian.Docs.Abstractions

构建期文档 Definition，包含 `IDocumentGenerator`、输入、事件映射和结构化诊断定义。适用于全部 profile，不依赖具体 Provider。实现、表面及安装边界统一见 [Qian.Docs](../Qian.Docs/README.md)。

## 模型体验

结果含源文件、行号与错误码，事件映射区分声明、生产方和消费方。

## 已知限制与延期项

本包不读取源码、不执行组件、不提供业务服务；由 Provider 完成静态生成。
