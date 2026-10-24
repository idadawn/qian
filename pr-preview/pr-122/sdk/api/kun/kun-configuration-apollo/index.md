> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Namespace Kun.Configuration.Apollo

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Configuration.Apollo` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Configuration.Apollo`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Configuration.Apollo)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

### Classes

[ApolloConfigurationExtensions](/pr-preview/pr-122/sdk/api/kun/kun-configuration-apollo/apolloconfigurationextensions.md)

显式添加 Apollo；源顺序由宿主按 appsettings、Apollo、Secret、环境变量、命令行安排。

[ApolloConfigurationProvider](/pr-preview/pr-122/sdk/api/kun/kun-configuration-apollo/apolloconfigurationprovider.md)

通过公开协议获取服务级配置；无网络且无缓存时启动失败，不以空配置继续。

[ApolloConfigurationSource](/pr-preview/pr-122/sdk/api/kun/kun-configuration-apollo/apolloconfigurationsource.md)

Apollo 标准 IConfiguration 源。

[ApolloOptions](/pr-preview/pr-122/sdk/api/kun/kun-configuration-apollo/apollooptions.md)

平台／服务级 Apollo 引导配置；不承载租户级配置或明文业务密钥。
