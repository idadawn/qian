> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# 发布、灰度与回切

## 发布保护

- **强制验签**：未签名或签名不符的制品直接安装失败（[qian.lifecycle](/pr-preview/pr-181/components/platform/qian.lifecycle.md)）。代码制品用 cosign 分离签名，组件文件夹的 digest 清单用 Ed25519 分离签名（`qian sign`，见 [qian 命令](/pr-preview/pr-181/cli/commands.md)）。
- **修订**：平台级流程是 制品 → 修订 → 激活；每次激活保留修订，可回切。

## 修订回切

`qian.lifecycle` 的 `Rollback(toRevision)` 回到指定修订；租户级的升级、卸载与清除逐租户执行，钩子幂等。

## 灰度发布

- 按实例灰度：`IFeatureFlags` 灰度开关（Apollo Label 灰度只按实例），配合 profile 滚动更新。
- 按租户灰度：靠租户开关，即在 `tenant:<id>` scope 挂载或停用条目（见 [scope](/pr-preview/pr-181/start/concepts/scope.md)）。
- 发行版随 CalVer 发布，插件独立灰度；Argo CD 以 `prune: false` 起步（[ADR-017 D17.9](/pr-preview/pr-181/design/adr/adr-017-deployment.md)）。
