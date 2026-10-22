# Qian.Scheduler.Abstractions

拥有 Scheduler 服务键、IScheduler、JobOptions、misfire/并发策略与错误词汇。行为、数据库边界和完整用法以 [Provider 主文档](../Qian.Scheduler/README.md) 为准。

## 模型体验

ScheduleOnce、ScheduleCron、ScheduleInterval 与 Cancel 都要求可信调用上下文。作业定义 Schema 随本包嵌入，能力 URI 只能选当前可见的已登记 local 作业。

## 已知限制与延期项

本包不实现持久化、回调或管理 API，不承诺任意外部副作用恰好一次。
