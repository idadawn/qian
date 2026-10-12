# 插件 SDK

- 遵循组件模型 §3、§5 和 S1-RT-014；主文档为 README.md。
- 只依赖 Runtime、Kun 与 Definition，不引入任何平台或组件 Provider。
- 能力元数据来自代码声明和源生成 JsonTypeInfo，不能从请求输入授予权限、主体类型或审计豁免。
- command/dispatching 属于 SDK 执行协议；实际执行端必须发起它，不能以测试自建事件替代。
- 单一 plugin.json 和既有 tools/metadata 表面；不产生第二份派生清单。
