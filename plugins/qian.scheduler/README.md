# qian.scheduler

组件的真实实现与主文档位于 [Qian.Scheduler](../../packages/Qian.Scheduler/README.md)，Definition 为 Qian.Scheduler.Abstractions。默认进入 platform/base，可在 standalone 组装；global 挂载的服务按 tenant scope 分区。

本目录包含唯一清单、配置 Schema 与作业定义 Schema。清单由代码派生；x-skeleton 只表示正式源发布与真实 qian add scheduler 安装门尚未通过，不表示 Provider 未实现。本地独立宿主和多副本测试不替代该安装门。

## 模型体验

仅调用 QianJob 显式登记且当前可见的能力 URI；作业定义 Schema 机器可读，任意 URL 被拒。

## 已知限制与延期项

callback、管理 API、run 事件及 staging 扫描属于 S1-SCH-002，正式包源安装在发布后复验。
