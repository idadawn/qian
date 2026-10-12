# SDK 源生成器

- 使用 IIncrementalGenerator、语义符号和源生成 JsonTypeInfo；不在生成器中执行组件代码、写项目文件或联网。
- 不以正则从 C# 源码提取安全声明；不支持的表达式明确诊断，不能静默生成空权限或空服务键。
- 生成源由编译器管理；磁盘清单合并由工具显式完成，只维护唯一 plugin.json 和已有表面。
- 诊断同步 AnalyzerReleases；公共 API 同步 PublicAPI 文件。
