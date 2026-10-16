# Profile 镜像的交付输入与恢复条件

本目录记录 profile 镜像的真实输入和验收准备。2026-10-14 产品负责人确认 DEP-003 保持 blocked；2026-10-16 按 R106 拆为两段：

- S1-DEP-003 交付镜像构建与供应链流水线，用 CI 内构建、不发布的自检镜像做端到端验证，现在即可推进；
- S1-DEP-008 把三个业务镜像接入流水线并复验。原 DEP-003 的三项验收原文移到这里，等下表的前置交付。

## 已核对的输入

| 镜像 | 既定形态 | 当前缺口 | 负责的前置任务 |
|---|---|---|---|
| qian-platform | JIT，包含 shell 与 model-gateway 组合包 | 现有 profile 是静态装配基线；RP、外壳静态制品和完整业务装配尚未交付 | S1-IDP-005、S1-YUE-001 及 profile 的业务组件任务 |
| qian-identity | JIT | Qian.Identity.Server 与可启动的唯一 issuer 宿主尚未交付 | S1-IDP-002 |
| qian-gateway | NativeAOT、distroless | Qian.Gateway 的认证、换签与路由宿主尚未交付 | S1-GW-001；依赖 S1-IDP-005 |

证据来源是仓库现状：[profile 装配基线](../../profiles/README.md) 明确其它 profile 的业务叶节点仍为 `x-skeleton`，只有 minimal 的真实 Runtime 启动已验收；`apps/` 当前仅有前端验证应用，相关身份、网关与外壳任务仍未完成。上述表格表示交付缺口。基线探针、minimal、Storybook 页面和 DEP-003 的自检镜像，都不能替代三个业务镜像。

## 可以固定的构建与验收约定

1. 构建入口、运行镜像模板与门禁由 S1-DEP-003 先行交付，用自检镜像验证；三个宿主与组件闭包就绪后，在 S1-DEP-008 接入各自的发布入口。依赖与构建版本锁定，源码 SHA、KunVersion 和 profile 修订均写入构建证据。
2. 多阶段构建；platform/identity 使用 JIT，gateway 必须完成 Linux NativeAOT 发布并使用 distroless 运行镜像。以非 root UID 启动，以只读根文件系统运行，只为明确需要写入的目录提供临时卷。
3. profile、组合包、组件清单与对应 patch 随镜像发布；环境 overlay 以 ConfigMap 挂载。分别在源码树与镜像内执行同一 dump-config 合成，比较解析后的结构与来源层，阻止配置漂移。
4. 镜像构建成功后按不可变 digest 生成 Syft SBOM，执行漏洞扫描，生成 Cosign 签名和 SBOM 关联证明。验签必须约束 CI 发布者身份/issuer，不能只确认某处存在一个签名文件。
5. 每个镜像保留 digest、构建 SHA、SBOM、扫描报告、签名验证结果、非 root/只读运行证据和配置比对结果。shell 的实际静态制品必须存在于 platform 镜像，并在真实 gateway 路由下可读取。
6. S1-DEP-005 继续负责发布、迁移先行、Argo 收敛和真实平台 PG/Redis/RabbitMQ/Apollo 联调。DEP-003 与 DEP-008 的供应链验收都不替代该发布门。

## 接入真实镜像（S1-DEP-008）

前置任务完成后，重新核对三件事：有效 profile 中每个必需叶节点的交付状态、三个宿主的发布入口、shell 制品路径；核对无误后再认领 S1-DEP-008。三项验收保持 DEP-003 的原文，只有真实镜像及其验证证据全部齐全，才可勾选和关闭。截至 2026-10-16，没有构建、发布或签署任何业务镜像，也没有修改集群。
