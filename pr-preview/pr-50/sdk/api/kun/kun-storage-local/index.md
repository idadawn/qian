> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Namespace Kun.Storage.Local

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Storage.Local` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Storage.Local`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Storage.Local)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

### Namespaces

[Kun.Storage.Local.DependencyInjection](/pr-preview/pr-50/sdk/api/kun/kun-storage-local-dependencyinjection/index.md)

### Classes

[LocalFileStorageOptions](/pr-preview/pr-50/sdk/api/kun/kun-storage-local/localfilestorageoptions.md)

Configures [LocalFileSystemStorage](/pr-preview/pr-50/sdk/api/kun/kun-storage-local/localfilesystemstorage.md).

[LocalFileSystemStorage](/pr-preview/pr-50/sdk/api/kun/kun-storage-local/localfilesystemstorage.md)

[IFileStorage](/pr-preview/pr-50/sdk/api/kun/kun-storage/ifilestorage.md) implementation that stores objects on the local filesystem.
Intended for development, tests, and single-node deployments. Presigned URLs are
developer-visible query-string-token URLs — validation is host responsibility.
