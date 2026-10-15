# CLI 约定

- 当前提供静态 dump-config、doctor 的 profile 与实时表面诊断、docs generate、skill-only 文档模板初始化、市场目录解析与无代码组件来源锁定，以及开发中的批量代码安装/升级、本地列表与移除；文件读取和输出不得执行目标组件或解析 secretRef 或读取私钥。
- 复用 @qian/surfaces，规则改动必须更新 .NET 装配器与共同 TCK。
- 参数错误也输出统一 ErrorDetail，不拼 { error: ... }。

- 市场来源必须先验 Ed25519 签名与 digest，再复用组件校验器；不 checkout 或执行来源内容。未验证市场只允许显式选择的 community 无代码组件。
- `add` 已接入真实包管理器、声明 digest、实际还原归档复核和 `--surfaces` / `--no-code` 投影；私有源已发布组件的真实安装、运行与逆序卸载由 test/private-feed.e2e.mjs 验证（需 QIAN_FEED_TOKEN，不随 `pnpm test` 运行）。组合层复杂边界、一键简写、引擎版本兼容、包原生签名/provenance、租户 scope 与中断恢复仍未完成。不能用通用包或临时包测试替代真实命名 Provider、私有包族发布或平台权限验收。
- 读包令牌只经环境变量与 NuGet.Config 的 %VAR% 引用传递，不写文件、不出现在输出里。
- 包管理器停止状态不明确时，保留安装锁、日志和备份，不能一边有进程写文件一边回滚。市场校验不得因代码安装而放宽；反向权限、版本和来源均保留在可审阅计划中。
- 包管理器命令一律经 runPackageCommand 运行，超时取 packageCommandTimeout()（QIAN_PACKAGE_TIMEOUT，缺省 600 秒），不在调用处写死；只有真正超时的诊断提示加长超时。预检还原不加 --disable-parallel。
- 组合包通过真实 profile.bundles 组装，每个子组件独立过校验与信任等级检查。预检可从声明引用的实际包补齐 qian 内容，不改宿主依赖、不复制包源凭据；同名内容冲突不能被 force 绕过。表面投影保留原来源摘要并重新校验；父组件与 no-code 切换均不能绕过子组件 required 保护。组合层边界和包真实性未全验收前，不能提前关闭完整交付门。
