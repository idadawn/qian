# Qian.Scheduler

[S1-SCH-001](../../pm/tasks/S1-SCH-001.md) 的 PostgreSQL 持久调度 Provider。Definition 为 `Qian.Scheduler.Abstractions`，服务键为 `Scheduler`；代码只依赖 Runtime、SDK 与各 Definition，不依赖 Registry、Capabilities 或 Data Provider。

## 组件与安装边界

组件文件夹在 [plugins/qian.scheduler](../../plugins/qian.scheduler/README.md)：唯一 plugin.json、配置 Schema、机器可读的作业定义 Schema。Provider 以 global 条目挂载，API 按调用者 tenant scope 分区；默认属于 platform 的 base 组合包，也可在 standalone 中装配。同一 Provider 和 local 作业绑定同时用于独立宿主与平台宿主。

入口是 SchedulerComponent。宿主已有的 Data/Tenancy 服务就绪后，Apply 校验专用 worker 连接、注册 Scheduler 并启动领取循环；卸载撤销服务、取消执行、释放循环。配置可由 profile patch 替换；不在 Runtime 中增加业务调度逻辑。当前源码与本地独立宿主已覆盖，组件保留 x-skeleton 安装标记：正式 NuGet 源发布与真实 qian add scheduler 安装尚未完成，不能把清单派生或本地 ProjectReference 运行当作该门通过。包族发布后按 S1-DEC-018 补版本/摘要与真实安装证据再移除标记。

## 宿主与数据库

宿主通过 Data 的 AddQianData 分别登记 SchedulerDbContext（普通受限应用连接）和 SchedulerWorkerDbContext（独立 worker 登录连接），两者 UseKunSchema(SchedulerDbContext.Schema)。凭据来自宿主连接/secretRef；组件 config 只保存 workerRole，不能保存连接串。迁移 Job 以受限属主调用 IDataMigrator；在线启动只校验。

三张表 job_definitions、job_triggers、job_runs 都是 IMultiTenant，生成迁移自动安装 FORCE RLS。定义按 (tenant_id, business_key) 唯一；触发器与定义、运行与触发器均有含 tenant_id 的组合外键。定义保存参数、调度表达式、执行策略、创建组件与请求指纹；trigger 只保存领取元数据；run 按 (tenant_id, trigger_id, scheduled_at) 唯一。

迁移后在迁移阶段应用 SchedulerWorkerAccess.CreateMigrationSql(workerRole)。此接口沿用 Audit 的专用登录模式：角色必须已创建为 LOGIN、NOINHERIT、NOSUPERUSER、NOBYPASSRLS、NOCREATEDB、NOCREATEROLE、NOREPLICATION，且没有成员关系。它只修改本组件 trigger 的 restrictive policy；专用登录在空 tenant GUC 时可读 trigger，并只可更新 lease_owner、lease_until、generation、active_run_id。定义和运行表不授予任何权限。普通租户角色仍只看自己的三表；同时校验 current_user 与 session_user，SET ROLE 不能冒充 worker 登录。每次领取重查角色及精确列权限，额外授权同样拒绝。

EF 迁移由 tests/Qian.Scheduler.Tests 的设计时入口经同一 AddQianData 模型生成。设计时生成器只把迁移类生成为 internal，不改模型与 SQL；生成后不手改迁移文件。

```bash
dotnet ef migrations add <名称> --project packages/Qian.Scheduler --startup-project tests/Qian.Scheduler.Tests --context SchedulerDbContext --configuration Release
```

## API 与作业目录

IScheduler 提供 ScheduleOnce、ScheduleCron、ScheduleInterval、Cancel。caller 必须是与 Kun ITenantContext 一致的 tenant scope；创建和执行都核对租户仍为 Active/Trial。业务键在租户内幂等：相同请求返回原定义，内容改变返回 CONFLICT；已取消的同键不会被隐式复活。一次性与 interval 的首次时间参与指纹；cron 的首次发生点以首次成功登记为准。同键不同租户互不影响。

目标使用既有能力 URI component/name，例如 qian.example/expire；不是 HTTP URL。SDK 在已有 Capability 的 partial 请求上增加 QianJob("code")，源生成 IJobRequest、静态作业代码和 metadata/jobs.json。组件 Apply 调用 ctx.ProvideJob<Request, Response>()，同时注册真实 ExecutableCapabilities 绑定和 LocalJobs 调度标记；两者都随效果栈撤销。目录包含目标、类型和真实输入/输出 Schema。能力目录与 local 作业目录的交集才可调度；普通能力没有显式 QianJob 注册时不可被调度。

创建与执行都检查当前 scope/restrict 下的两项绑定，必须引用同一个 CapabilityContract；任意 URL、未登记、被隐藏与卸载目标都不能执行。输入按能力 Schema 校验；写作业的 idempotencyKey 由调度器覆盖为稳定 run id，创建参数不能改写此身份。CreatedBy 保存创建组件，仅是审计来源；执行者是 scheduler，不持久保存或冒充创建者的用户令牌。Runtime restriction 按 scope 生效，不依赖创建上下文对象；处理器继续经过实际能力绑定及其 Mediator 权限策略。

## 时间、并发与恢复语义

- cron 为 UTC 6/7 段，秒、分、时、日、月、星期、可选年；使用 Quartz 的表达式解析器，不启动另一套调度器。固定间隔保持首次时间的节拍，不从完成时间累积漂移；最小间隔一秒。
- 每副本每秒一次领取。短事务 FOR UPDATE SKIP LOCKED 后写租约和递增 generation；run id 在第一次领取生成，崩溃接管或失败重试复用。数据库时间判断到期/租约，普通应用连接只在从受保护记录取得的租户范围内读取参数和写运行。
- 默认租约30秒，每租约三分之一续租；配置允许3至3600秒。默认每副本16个并行作业，范围1至100。领取、续租、完成和推进全部受 owner/generation/尚未过期租约保护，旧执行者不能覆盖新 owner。
- MisfirePolicy：Skip 跳过超过宽限的发生点；FireOnce 合并积压补一次；CatchUp 保留原计划时间逐次补齐。默认 FireOnce，宽限5秒。恢复已有 run 是重试，不因迟到而丢掉尚未确认的业务效果。
- JobConcurrency：Queue 串行处理发生点，按 misfire 处理积压；Skip 在完成后跳过执行期间的重叠发生点。两者都不并行执行同一定义；不同定义可并行。
- 默认超时1分钟（100毫秒至1天）、最多3次尝试（1至100）、重试间隔5秒（0至1天）。超时给处理器发送取消。处理器若不响应，仍续租并占用串行位置，不虚报已强制终止。停机最多等5秒；未合作的处理器需由进程生命周期最终终止。
- Cancel 持久归档 trigger、递增 generation 并将活动 run 标为 cancelled；返回后不再领取。已有处理器在下一次续租检查时收到取消；取消不是回滚已完成业务效果。旧执行者收尾不能改写 cancelled。
- 正常停止 worker 时，取消正在执行的处理器并停止续租，不执行完成/推进事务；同一 active_run_id 和发生点保留，由另一 worker 在租约到期后接管。Runtime 公共卸载原有的关闭上下文围栏同样保护持久状态，调度器现在显式处理停机，不再依赖关闭上下文的异常路径。

无故障分钟验收同时验证逻辑 run 与真实处理器调用次数。故障恢复语义是至少一次尝试：业务副作用完成后、run 完成回写前崩溃，会以同一 run id 再次调用。job_runs 唯一键不证明任意外部副作用恰好一次，写处理器必须在业务事务或外部系统用 idempotencyKey 去重。测试刻意在此窗口杀进程，分别断言两次 attempt 与一次幂等 effect。

## 验证

专项复用 Qian.Testing.Containers.PostgresFixture，不重复创建容器框架。正常调度运行3个独立进程、两个租户、100个真实每分钟触发器，连续两个分钟各100次；实际杀进程检查租约到期接管。另覆盖业务键并发与取消、超时/重试、misfire、RLS原始SQL、组合外键、SET ROLE与成员关系、过量列权限、过期租约/旧owner、租户等待期冻结和目录过滤。SDK/ManifestTool 用真实 Roslyn 与子进程验证生成、同步、漂移、手改保护、多组件归属和删除。

```bash
QIAN_REQUIRE_DOCKER=1 dotnet run --project tests/Qian.Scheduler.Tests -c Release
dotnet run --project tests/Qian.Plugin.Sdk.Tests -c Release
```

## 模型体验

作业定义 Schema 位于 plugins/qian.scheduler/schemas/job-definition.schema.json；代码目录 Schema 位于 schemas/v2/scheduler-jobs.schema.json。目录来自真实代码与当前可见绑定，未登记 URI 与任意 URL 明确拒绝。错误使用统一 ErrorDetail 与 Definition 中的 SCHEDULER_MISCONFIGURED，不输出参数、连接凭据或完整异常作为业务响应。

## 已知限制与延期项

callback、S2S/HMAC、结果回调、管理 API、jobs.create/run-now 能力、run 集成事件、完整作业租户传播验收及 staging 到期扫描属于 S1-SCH-002。核心已在可信受保护记录的租户范围执行 local 处理器，不开放任意 URL、脚本或数据接口任务。本任务不承诺外部副作用恰好一次、不强制中断任意 .NET 代码、不以本地验收替代正式包源安装或 staging。
