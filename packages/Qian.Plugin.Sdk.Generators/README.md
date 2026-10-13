# Qian.Plugin.Sdk.Generators

SDK 的 Roslyn 增量源生成器，权威使用与交付边界见 [SDK 主文档](../Qian.Plugin.Sdk/README.md)。本包不在生成阶段写文件、加载业务组件或联网。CapabilityGenerator 生成强类型能力契约；ComponentMetadataGenerator 静态分析服务注册并生成构建期清单工厂。未支持且无法证明的表达式报编译诊断。
