> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# 市场与 ISV

**阶段**：S3 对第三方开放；S3 之前本页只写规则（[ADR-014](/pr-preview/pr-202/design/adr/adr-014-marketplace.md)、[组件模型规格](/pr-preview/pr-202/design/prd/specs/component-model.md) §8、§11）。

## 应用与分发

- **应用 = 组合包 + 市场条目**。市场条目只有 `name`、`source`、`policy`、`category`、`interface`，字段见[目录市场 marketplace.json](/pr-preview/pr-202/cli/marketplace-json.md)。
- 两种分发：`managed`（托管、锁定、可升级，租户在上层叠加定制，默认）与 `template`（拷贝即拥有，可改）。
- `managed` 升级做三方比较（旧包、新包、租户层），冲突项由租户选择保留、采用新版或手工合并；升级前快照，可回滚。

## 信任等级

| 等级                    | 允许的代码形态                                            | 运行位置                  |
| --------------------- | -------------------------------------------------- | --------------------- |
| `system` / `official` | 全部形态                                               | 共享进程（profile 宿主）      |
| `partner`             | `package`、`function`、`ui`；`service` 在 S3 按沙箱策略另行开放 | 独立工作负载 / Wasm / 浏览器隔离 |
| `private`             | `package`、`function`、`ui`                          | 同上                    |
| `community`           | 同 `private`；S1 只接受无代码组件                            | 同上                    |

第三方代码永不进共享进程；无代码组件对所有等级开放。S3 不开放第三方 `service` 上架。

## 签名与审核

- 按信任等级强制验签：OCI 制品 cosign 分离签名，组件文件夹 digest 清单 Ed25519 分离签名；市场文件本身也签名，公钥须命中客户端信任根。
- 未签名或未命中信任根的条目需 `--allow-unverified`，只允许无代码组件、等级最高 `community`。
- 审核分级：免费元数据模板快审，付费或带代码的走安全审查，权限扩大触发复审。

## 私有市场

项目可在 `.qian/config.yml` 里配置多个市场并设优先级，`qian add` 按优先级查找；企业或租户的私有市场与官方市场并列，额外信任的公钥写进 `trust.keys`。
