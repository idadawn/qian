# Qian.Surfaces.Abstractions

声明式表面的 Definition：文档快照、校验结果、行号诊断、错误码，以及 `ISurfaceReader` / `ISurfaceValidator` 端口。仅依赖运行时，不依赖具体校验 Provider。

## 装配位置

所有 profile 可使用；对应 Provider 为 `Qian.Surfaces`，运行时目录键由 `Qian.Runtime.SurfaceKeys` 拥有。此包是 CLI `qian add` 与装载器的代码依赖，不是独立业务组件。

## 模型体验

诊断使用标准 ErrorDetail，源文件位置放在外层与 pointer；不会返回执行结果。错误码由 `SurfaceErrors.All` 提供，供统一目录登记。

## 已知限制与延期项

本包只有契约，不进行 I/O 或执行表面。具体校验语义与使用方式见 [Provider 文档](../Qian.Surfaces/README.md)。
