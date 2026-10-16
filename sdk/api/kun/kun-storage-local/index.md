> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Storage.Local

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage.Local` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage.Local`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Storage.Local)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Namespaces

[Kun.Storage.Local.DependencyInjection](/sdk/api/kun/kun-storage-local-dependencyinjection/index.md)

### Classes

[LocalFileStorageOptions](/sdk/api/kun/kun-storage-local/localfilestorageoptions.md)

Configures [LocalFileSystemStorage](/sdk/api/kun/kun-storage-local/localfilesystemstorage.md).

[LocalFileSystemStorage](/sdk/api/kun/kun-storage-local/localfilesystemstorage.md)

[IFileStorage](/sdk/api/kun/kun-storage/ifilestorage.md) implementation that stores objects on the local filesystem.
Intended for development, tests, and single-node deployments. Presigned URLs are
developer-visible query-string-token URLs — validation is host responsibility.
