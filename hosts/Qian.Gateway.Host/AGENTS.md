# gateway 业务宿主

- 这里只做真实 Provider 组合，不复制身份、网关、Data 或 Audit 实现。
- 启动只校验；数据库迁移在独立 Job，不关闭 RLS、不用管理登录跑在线请求。
- 引导连接凭据仅从指定 Secret 文件读取；Identity 客户端密钥经 Config 的权限策略和真实平台审计解析。
- 原生 Provider 接口按 DEC-030 的配套任务消费；未获得完整 Linux 发布和运行证据前不恢复 AOT 声明。
- 生产 DataProtection 密钥目录必须持久且在副本间共享。
