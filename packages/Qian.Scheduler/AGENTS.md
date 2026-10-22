# 调度 Provider

- 主文档为 README.md；保持三表 FORCE RLS、复合外键与 worker 专用登录/列权限边界。
- 只依赖 Runtime、SDK 和 Definition；Data 的装配由宿主完成，不引用 Data/Registry/Capabilities Provider。
- 领取、续租、完成和推进都检查未过期租约及 owner/generation；取消后旧执行者不能回写。
- 从真实可见的 QianJob/能力绑定执行；不从 URL、清单文本或缓存 handler 授予执行权限。
- 迁移由设计时入口生成，不手改；测试复用公共容器并真杀进程，分钟验收不能改成秒。
- 不把 run 唯一键描述成任意外部副作用恰好一次；callback 与 staging 留在 SCH-002。
