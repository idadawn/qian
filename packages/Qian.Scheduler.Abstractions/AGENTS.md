# 调度 Definition

- 契约归属与语义见 Qian.Scheduler/README.md；不引用 Provider。
- 新增公共 API 登记 PublicAPI，错误码在本包定义。
- 取消与超时采用合作式边界，不能承诺回滚已发生副作用。
