# Profile 镜像的交付输入与恢复条件

本目录记录 S1-DEP-003 的真实输入和验收准备；2026-10-14 经产品负责人确认，本轮保持四任务范围，缺少的业务宿主由其原任务交付，DEP-003 保持 blocked。

## 已核对的输入

| 镜像 | 既定形态 | 当前缺口 | 负责的前置任务 |
|---|---|---|---|
| qian-platform | JIT，包含 shell 与 model-gateway 组合包 | 现有 profile 是静态装配基线；RP、外壳静态制品和完整业务装配尚未交付 | S1-IDP-005、S1-YUE-001 及 profile 的业务组件任务 |
| qian-identity | JIT | Qian.Identity.Server 与可启动的唯一 issuer 宿主尚未交付 | S1-IDP-002 |
| qian-gateway | NativeAOT、distroless | Qian.Gateway 的认证、换签与路由宿主尚未交付 | S1-GW-001；依赖 S1-IDP-005 |

证据来源是仓库现状：[profile 装配基线](../../profiles/README.md) 明确其它 profile 的业务叶节点仍为 `x-skeleton`，只有 minimal 的真实 Runtime 启动已验收；`apps/` 当前仅有前端验证应用，相关身份、网关与外壳任务仍未完成。上述表格表示交付缺口，不把基线探针、minimal 或 Storybook 页面作为三个业务镜像的替代品。

## 可以固定的构建与验收约定

1. 三个宿主与组件闭包就绪后，再确定 Docker 构建入口。依赖与构建版本锁定，源码 SHA、KunVersion 和 profile 修订均写入构建证据。
2. 多阶段构建；platform/identity 使用 JIT，gateway 必须完成 Linux NativeAOT 发布并使用 distroless 运行镜像。以非 root UID 启动，以只读根文件系统运行，只为明确需要写入的目录提供临时卷。
3. profile、组合包、组件清单与对应 patch 随镜像发布；环境 overlay 以 ConfigMap 挂载。分别在源码树与镜像内执行同一 dump-config 合成，比较解析后的结构与来源层，阻止配置漂移。
4. 镜像构建成功后按不可变 digest 生成 Syft SBOM，执行漏洞扫描，生成 Cosign 签名和 SBOM 关联证明。验签必须约束 CI 发布者身份/issuer，不能只确认某处存在一个签名文件。
5. 每个镜像保留 digest、构建 SHA、SBOM、扫描报告、签名验证结果、非 root/只读运行证据和配置比对结果。shell 的实际静态制品必须存在于 platform 镜像，并在真实 gateway 路由下可读取。
6. S1-DEP-005 继续负责发布、迁移先行、Argo 收敛和真实平台 PG/Redis/RabbitMQ/Apollo 联调。DEP-003 的供应链验收不替代该发布门。

## 恢复 DEP-003

前置任务完成后，重新核对有效 profile 中每个必需叶节点的交付状态、三个宿主的发布入口及 shell 制品路径，再认领恢复。原三项验收保持不变，只有真实镜像及其验证证据全部齐全才可勾选和关闭。本次未构建、发布或签署业务镜像，也未修改集群。
