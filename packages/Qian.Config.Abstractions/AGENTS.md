# Qian.Config.Abstractions

- 本包定义 Config seam 的只读端口、错误词汇、事件与审计能力 ID、权限点常量；S1-CFG-004 / S1-DEC-031 另定义可选 FeatureFlagDrafts 草稿管理契约。Config 本身仍无写方法，管理输入不能接收任意配置键、地址、namespace、操作员或 Token；不实现 Apollo、租户配置或密钥存储。
- 事件载荷与审计相关类型不得携带配置值。
- 原始配置读取是受信任代码接口，不能自动发布为模型工具；秘密解析权限仍由专属任务完成。
- 修改公共 API 同步 PublicAPI 清单和 Provider 的真实验证。
