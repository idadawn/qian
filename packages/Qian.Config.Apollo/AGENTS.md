# Qian.Config.Apollo

- 复用已发布 Kun.Configuration.Apollo，不复制协议客户端、缓存或签名实现。
- 顺序固定为 appsettings、Apollo 公共/插件/application、K8s Secret 文件、环境变量、命令行。
- AccessKey 引导值只能是引用，解析回调每次签名前调用，凭据不入快照、日志或提交。
- Apollo 仅提供平台/服务级配置，不能用它保存租户设置。
- IConfigurationRoot 与配置源由创建方释放；借用宿主配置根的插件只撤销自己的登记。
- 真实 Apollo 验收复用共享测试 fixture，不能把协议替身当成真实服务通过的证据。
