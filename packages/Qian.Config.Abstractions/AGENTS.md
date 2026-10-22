# Qian.Config.Abstractions

- 本包只定义 Config seam 的只读端口、错误词汇、事件与审计能力 ID、权限点常量；不实现 Apollo、租户配置或密钥存储。
- 事件载荷与审计相关类型不得携带配置值。
- 原始配置读取是受信任代码接口，不能自动发布为模型工具；秘密解析权限仍由专属任务完成。
- 修改公共 API 同步 PublicAPI 清单和 Provider 的真实验证。
