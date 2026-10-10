# 文档 Definition 约定

公共 API 必须登记；只保留端口、输入输出与错误码，不引入 Provider。

## 模型体验

沿用 ErrorDetail 及表面诊断外层，事件映射必须保留源码位置。

## 已知限制与延期项

读取、分析、模板和文件写入逻辑属于 Provider 或 CLI。
