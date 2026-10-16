> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# 密钥

## secretRef

- 形状：`secretRef://<scope>/<path>`，`scope` 取 `platform`、`tenant`、`tenant/<id>`、`dev`。清单、表面、patch、事件、日志与 `dump-config` 输出里只允许这种引用，永不出现明文。
- 解析：由配置 seam 的 `ISecretResolver` 在**使用时**解析（Apollo 引用 K8s Secret）；组件在 `policy.permissions.secrets[]` 声明允许解析的范围，越界被拒并记审计。
- 租户密钥：`secretRef://tenant/<id>/…` 只能被同一租户的上下文解析，RLS 与解析器双重检查。
- 校验：疑似密钥键名的明文值与高熵值直接报错（`qian doctor`）。

详见[组件模型规格](/pr-preview/pr-181/design/prd/specs/component-model.md) §11.3。

## 存放与注入

- 仓库只存模板、键名与 `secretRef`；Kustomize 只注入引导项（`Apollo__*` 与 Secret 文件）。
- 初始化脚本「只建缺失、不覆盖、不打印」（[ADR-017 D17.10](/pr-preview/pr-181/design/adr/adr-017-deployment.md)）。
- 模型供应商密钥同样只以 `secretRef` 出现（ADR-015 D15.11）。

## 轮换

- 网关签名私钥热轮换，JWKS 双钥重叠（[ADR-010](/pr-preview/pr-181/design/adr/adr-010-gateway-layers.md)）。
- 市场信任根支持多把公钥重叠轮换；移除的公钥签过的旧市场快照随即失效。
