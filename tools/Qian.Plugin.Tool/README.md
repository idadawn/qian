# 组件清单构建工具

对显式选择的本地可信编译程序集执行 check 或 sync；使用及信任边界见 [SDK 主文档](../../packages/Qian.Plugin.Sdk/README.md)。它负责把代码派生字段合入唯一 plugin.json 与既有工具表面，不安装组件，不执行 Apply，不作为未知制品的安全校验器。

`inventory <程序集>` 只读列出实际 IQianPlugin 类型与组件 ID，供 `scripts/check_plugin_manifests.py` 动态发现；未注解的实现必须在 `scripts/plugin-implementations.json` 分类。工具保持源 Schema 与 ErrorDetail 协议，权限数组不添加额外字段。
