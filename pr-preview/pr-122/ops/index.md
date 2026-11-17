> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# 运维

可部署单元是 profile。Qian 沿用现有 K3s / Argo CD / Kustomize / Harbor 约定，在独立命名空间里运行，带专属的 PostgreSQL 16 与 Redis 7（[ADR-017](/pr-preview/pr-122/design/adr/adr-017-deployment.md)）。

| 主题                                                | 内容                                                                                                                                                                  |
| ------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [GitOps 部署](/pr-preview/pr-122/ops/deploy.md)     | Argo CD app-of-apps、Kustomize base / overlay、按 digest 写回、迁移 Job 先行                                                                                                  |
| [发布、灰度与回切](/pr-preview/pr-122/ops/release.md)     | 制品验签、修订回切、灰度开关与 profile 滚动更新                                                                                                                                        |
| [密钥](/pr-preview/pr-122/ops/secrets.md)           | `secretRef`、解析与授权、轮换                                                                                                                                                |
| [模型网关接线](/pr-preview/pr-122/ops/model-gateway.md) | GPUStack → Higress、逻辑模型名、配额与计量（S1 可选）                                                                                                                               |
| 配置中心                                              | Apollo 的配置源顺序与热更新，见 [qian.config](/pr-preview/pr-122/components/platform/qian.config.md) 与 [ADR-007](/pr-preview/pr-122/design/adr/adr-007-config-center-apollo.md) |
| 监控与审计                                             | OTel 与计量见 [qian.observability](/pr-preview/pr-122/components/platform/qian.observability.md)，审计见 [qian.audit](/pr-preview/pr-122/components/platform/qian.audit.md) |

每个服务提供 `/healthz`（存活）与 `/readyz`（就绪，含 PG / Redis / RabbitMQ / Apollo 依赖检查）。
