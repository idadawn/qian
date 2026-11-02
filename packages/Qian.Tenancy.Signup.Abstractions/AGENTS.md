# 租户自助开通 Definition

- 本包只声明服务键、申请与审核 DTO、状态机描述、`SIGNUP_*` 错误码与发件端口 `ISignupMessenger`；实现与主文档在 Qian.Tenancy.Signup。
- 只依赖 Qian.Runtime；不引用任何 Provider，也不直接依赖 Kun 实现包。
- 输入 DTO 拒绝未知字段，不接受任何租户字段；带密码、验证码、令牌或手机号原值的类型重写 ToString，不进日志。
- 错误码只增不改含义；新增码同步 `TenancySignupErrors.All`（组件装载时逐个登记）与测试。公共 API 变更同步 PublicAPI 文件。
