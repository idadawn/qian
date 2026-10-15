# Qian.Data

Data seam 的 Provider，配合 Qian.Data.Abstractions 的安全上下文、服务键和会话契约。参考应用数据验收已补齐，SDK / CLI 的完整代码安装仍有后续门禁，当前进度见 [任务](../../pm/tasks/S1-DATA-001.md)。

## 跨宿主租户模型

租户查询过滤器必须从当前执行查询的 DbContext 读取租户。相同上下文类型和租户在不同宿主中复用 EF 模型时，不能保留第一次建模时的访问器实例；旧访问器退出或切换租户不应改变新请求的条件。`CrossHostTenantModelTests` 以真实受限 PostgreSQL 角色验证模型复用，并让第二宿主编译不同形状的查询，覆盖旧查询缓存可能掩盖的问题。Qian 通过唯一 `KunVersion` 消费此框架修复，仍保留 RLS 双拦截器与模型缓存门禁。

## 安全装配

```csharp
services.AddQianData<AppDbContext>((sp, options) => options
    .UseNpgsql(connectionString).UseKunSchema("plugin_example"));
```

业务上下文继承 QianDbContext。独立宿主可直接注入 IDataService，无需挂载平台插件。装配复用当前 KunVersion 的 AddKunTenantRls、AddKunDataConventions 与租户字段拦截器，强制模型缓存键、Qian 模型定制器、应用角色检查和迁移生成器。派生上下文漏调 base 仍保留租户/软删过滤，并与业务过滤条件取交集；采用 snake_case、Guid v7、可空审计主体和 xmin。每个上下文使用同一个 plugin_ 前缀 schema，租户列非空且默认具有索引。

启动会构建模型并打开真实连接，只读取角色属性，不建表或迁移；上下文带有迁移时还核对数据库没有未应用的迁移（见下文多副本迁移协调）。登录角色及当前角色必须均为 NOSUPERUSER / NOBYPASSRLS，预打开连接也检查；同步/异步新开连接遇高权限角色立即拒绝并关闭。

迁移生成器为目标模型中的每个新建 IMultiTenant 表紧接着输出 ENABLE / FORCE RLS 与 USING / WITH CHECK。租户限制采用 AS RESTRICTIVE，附加宽松策略不能通过 OR 放宽边界；`NULLIF(current_setting('app.current_tenant_id', true), '')` 使缺失和空租户不匹配。丢失目标模型或标准租户列标记时拒绝生成裸表。历史表转换仍须显式安全迁移。PgBouncer 事务池仍要求显式事务。

## 数据服务与工作单元

DataPlugin 在 Apply 中提供 Data 键；消费者通过 `ctx.Data()` 解析。每次 `OpenAsync<TContext>()` 建立独立 DI scope，选择已注册的业务上下文，把该上下文、Kun IntegrationEventBuffer 和 EfUnitOfWork 绑定在一起。不会把全局 KunDbContext 指向最后登记的业务上下文。

```csharp
await using var session = await ctx.Data().OpenAsync<AppDbContext>(ct);
session.Context.Add(entity);
session.Outbox.Enqueue(integrationEvent);
await session.SaveChangesAsync(ct);
```

事件类型必须由宿主显式通过 AddKunEventBus 登记，推荐使用源生成 JsonTypeInfo；Data 不暗中登记空目录。IOutboxWriter 与 IIntegrationEventBus 必须指向同一个 scoped 实例：默认是 Kun 累加器；装了事件组件时换成它的信封写入端，该写入端经 IOutboxBufferBinding 声明自己写入当前 scope 的缓冲器，否则拒绝开会话。领域通知走 Kun Mediator 的当前 scope，通知处理器与工作单元拿到同一业务上下文和缓冲；存在通知但未装配 Mediator、通知没有实现 INotification 时拒绝保存。

保存失败后会话不可复用，调用方必须释放后新开；业务数据与 Outbox 在同一次 EF 保存中提交或回滚。会话创建后不可切换租户，混入其他租户的事件会被拒绝。卸载 Provider 会撤销入口并释放存活会话，已缓存的数据服务不能再开新会话。本组件负责 Outbox 入库，后台投递由 [Qian.Events](../Qian.Events/README.md) 负责；AddQianData 同时登记 IDataContextCatalog，投递服务据此逐个处理各业务上下文的 Outbox 表。

## 多副本迁移协调

`IDataMigrator.MigrateAsync` 随 AddQianData 登记，逐个业务上下文处理：

1. 在迁移属主连接上取 `pg_advisory_lock`，键由 schema 派生（与连接串、宿主无关），同一插件的迁移在所有副本间互斥，不同插件互不阻塞；
2. 锁内读待应用的 EF 迁移并执行，再确认已无遗漏；
3. 释放会话级锁后归还连接。

同时启动的副本只有一个执行 DDL；其余在锁上等待，拿到锁后发现没有待应用的迁移，只做校验。等锁上限默认 10 分钟。

迁移连接由 `AddQianDataMigrations(options => options.ResolveConnection = (sp, contextType) => ...)` 提供，返回 null 时用上下文自身的连接。属主角色同样必须是 NOSUPERUSER / NOBYPASSRLS（连接时由同一个角色拦截器检查）；它建的 schema 与表经默认权限授予在线应用角色：

```sql
ALTER DEFAULT PRIVILEGES FOR ROLE qian_owner GRANT USAGE ON SCHEMAS TO qian_app;
ALTER DEFAULT PRIVILEGES FOR ROLE qian_owner GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO qian_app;
```

K8s 迁移 Job 以 `--migrate-only` 启动宿主（`QianDataMigrationExtensions.IsMigrateOnly`），迁移完即退出。在线副本启动时只校验：上下文带有迁移而数据库还有未应用项时拒绝启动，提示先跑迁移任务。

迁移用 `dotnet ef migrations add` 生成，放在所属 Provider 包的 Migrations 目录；仓库 `.editorconfig` 把该目录按生成代码处理。迁移执行时仍经 Qian 迁移生成器，租户表自动带上强制 RLS。

### 迁移执行与 RLS 核查

租户表的 `ENABLE` / `FORCE ROW LEVEL SECURITY` 与两条策略（`qian_tenant_access` 宽松、`qian_tenant_guard` 限制）不写在迁移文件里，由 `QianPostgresMigrationsSqlGenerator` 在迁移**执行期**紧跟每条 `CREATE TABLE` 生成。所以迁移必须经 `AddQianData<T>` 登记的 `IDataMigrator` 执行，也就是上文以 `--migrate-only` 启动的迁移任务。

直接 `dotnet ef database update`（或 `dotnet ef migrations script` 后手工执行）走的是 `dotnet ef` 自己建的上下文：设计时入口没有经 `AddQianData<T>` 装配时，用的是 Npgsql 默认生成器，建出的租户表没有 RLS 与策略，租户隔离只剩查询过滤器。在线副本的启动检查会拒绝非 Qian 的迁移生成器，也会拒绝还有未应用迁移的数据库，但不检查已经建好的表有没有策略。曾绕过迁移任务建表的库，用下面的 SQL 逐表核查（以迁移属主或有权读 `information_schema` 的角色执行）：

```sql
-- 每张租户表（tenant_id 非空）应 rls、force_rls 都为 t，policies 恰为
-- qian_tenant_access(PERMISSIVE), qian_tenant_guard(RESTRICTIVE)；有任何一行不符，说明该表绕过了 Qian 迁移生成器。
SELECT c.table_schema, c.table_name, k.relrowsecurity AS rls, k.relforcerowsecurity AS force_rls,
       string_agg(p.policyname || '(' || p.permissive || ')', ', ' ORDER BY p.policyname) AS policies
FROM information_schema.columns c
JOIN pg_namespace n ON n.nspname = c.table_schema
JOIN pg_class k ON k.relnamespace = n.oid AND k.relname = c.table_name AND k.relkind = 'r'
LEFT JOIN pg_policies p ON p.schemaname = c.table_schema AND p.tablename = c.table_name
WHERE c.table_schema LIKE 'plugin\_%' AND c.column_name = 'tenant_id' AND c.is_nullable = 'NO'
GROUP BY c.table_schema, c.table_name, k.relrowsecurity, k.relforcerowsecurity
ORDER BY c.table_schema, c.table_name;
```

`dotnet ef` 的设计时入口要经 `AddQianData<T>` 取上下文，模型才与运行时一致（命名约定、租户标记、Outbox 表）。组件包不能为此依赖本包（依赖门禁只允许 Abstractions），所以 `IDesignTimeDbContextFactory<T>` 放在组件的测试项目里，以它作启动项目。以能力上下文为例（`tests/Qian.Capabilities.Tests/CapabilitiesDesignTimeFactory.cs`）：

```bash
dotnet ef migrations has-pending-model-changes --project packages/Qian.Capabilities --startup-project tests/Qian.Capabilities.Tests --context CapabilitiesDbContext
dotnet ef migrations add <名称> --project packages/Qian.Capabilities --startup-project tests/Qian.Capabilities.Tests --context CapabilitiesDbContext --output-dir Migrations
```

生成的迁移类改为 `internal`，不进入组件的公开 API。

## 分布式 ID

默认主键仍是 Guid v7。需要数值 ID 时调用 `AddQianSnowflakeIds`（宿主须登记 `IConnectionMultiplexer`），注入 `ISnowflakeIdGenerator`。ID 由 41 位毫秒时间戳、10 位 WorkerId、12 位序列组成。

WorkerId 经 Redis 租约分配：

- 启动时以 `SET qian:snowflake:worker:{n} 持有者 NX PX` 取最小空闲编号，后台按间隔比对持有者后续期，正常停止时释放；
- 本地只在「最后一次成功续租的发出时刻 + 租约时长」之前发号，Redis 上租约过期、编号可能被他人取得时，本副本已经停发；发现编号被他人持有时立即停发，改取空闲编号；
- 每个编号另记高水位 `qian:snowflake:worker:{n}:hwm`（已用或可能用到的最大毫秒），新持有者从高水位之后开始。

因此正常重启、崩溃后回收或机器间时钟不一致，都不会让复用的编号产生重复 ID。时钟回拨时沿用上次的毫秒继续递增序列。脚本同时操作租约与高水位两个键，要求单实例 Redis。

## 序号服务

`AddQianSequences((sp, options) => options.UseNpgsql(...))` 登记 `ISequenceService` 与 qian.data 自己的系统上下文（schema `plugin_qian_data`）。表 `sequence_rules` 与 `sequence_counters` 都是租户表、强制 RLS，结构随包内迁移交付。

规则模式形如 `'SO' + yyyyMMdd + seq(6)`，由 `+` 连接的段组成：

- 单引号字面量，1 到 32 个字符；
- 日期段：yyyy、yyyyMM、yyyyMMdd、yy、yyMM、yyMMdd、MM、dd、MMdd；全部日期段组成「周期」，周期变化时流水从 1 重新开始；
- 恰好一个 `seq(n)`，n 为 1 到 18。

`DefineAsync(ruleCode, pattern)` 定义或修改当前租户的规则；模式变化时修订号加一，各作用域的流水从 1 重新开始。`NextAsync(ruleCode, scopeKey)` 用一条 `INSERT … ON CONFLICT DO UPDATE … RETURNING` 原子取号，在自己的连接上自动提交：同一规则、修订、作用域与周期内严格递增、不重复、不跳号。号一经取出即占用，调用方的业务事务回滚不归还。日期按 `QianSequenceOptions.TimeZone` 计算，默认 UTC。

| 错误码 | HTTP | 含义 |
|---|---|---|
| DATA_SEQUENCE_RULE_INVALID | 422 | 规则编码、作用域键或模式不合法 |
| DATA_SEQUENCE_RULE_NOT_FOUND | 404 | 当前租户没有定义该规则 |
| DATA_SEQUENCE_EXHAUSTED | 409 | 当前周期的流水已用尽 |
| DATA_ID_WORKER_UNAVAILABLE | 503 | 本副本暂时没有有效的 WorkerId 租约，可重试 |

## 基准类型与方言

`AddQianDataTypes()` 登记 `IDataTypeCatalog` 与方言注册表 `IDataDialectRegistry`，不需要数据库。方言组件另行登记，PostgreSQL 是 [Qian.Data.Dialect.Pg](../Qian.Data.Dialect.Pg/README.md) 的 `AddQianPgDialect()`。

- 10 个系统基准类型（int、bigint、decimal、float、datetime、varchar、text、bool、json、uuid）来自 Definition 包嵌入的种子 base-data-types.json，与 Filter 运算符矩阵的字段类型一一对应。每个类型声明自己接受的约束（facet）：length、precision、scale、range。
- 方言以 `IDataDialect` 实现 qian.data.dialect/v1：描述数据（驱动、只作展示的连接串模板、每个基准类型一条映射）加连通探测。注册表按名称独占（R38，键为 `Data.Dialects:<name>`），释放句柄即撤销。
- 注册时核对映射：覆盖全部系统类型，约束与类型声明一致，区间不为负、下限不大于上限、缺省值落在区间内。不合规的方言拒绝注册。
- `MapField(dialect, field)` 按映射校验字段定义并补齐缺省值。以下情况抛 DATA_FIELD_DEFINITION_INVALID，errors[] 逐项指向 `#/length`、`#/precision`、`#/scale`、`#/minimum`、`#/maximum`：
  - 长度、精度、小数位超出区间；
  - 上下限超出原生类型可表示的范围（decimal 由精度 p 与小数位 s 推出 ±(10^(p−s) − 10^−s)，上下限的小数位不超过 s）；
  - 最小值大于最大值；
  - 给出类型不接受的约束。
- 只给精度时，缺省小数位随之收窄。
- S1 只有系统类型，`DeleteTypeAsync` 对它们一律返回 DATA_TYPE_PROTECTED；自定义类型随 S2 建模交付。

HTTP 端点见 [Qian.Data.AspNetCore](../Qian.Data.AspNetCore/README.md)。

## 数据连接档案

```csharp
services.AddQianPgDialect();
services.AddQianDataConnections((sp, options) => options.UseNpgsql(appConnection));
services.AddSingleton<ISecretAccessObserver>(auditSink);
services.AddQianSecrets(new SecretResolverOptions
{
    MountRoot = "/var/run/secrets/qian",
    Bindings = [new("secretRef://tenant/<租户>/data/warehouse", new("qian", "warehouse", "password"), "<租户>")],
    Policies = [new("qian.data", [new("data/*", "tenant")], [new("data/*", "tenant")])]
});
```

- 表 `data_connections` 与序号表同在 plugin_qian_data，是同一个系统上下文：`AddQianSequences` 与本方法共用，两处的连接配置必须解析出同一个连接串，否则创建上下文时拒绝。
- 它是租户表，强制 RLS；名称在租户内唯一，软删后可以复用；系统列沿用 AggregateRoot 约定（审计主体、xmin）。
- 档案记录方言、主机、端口、库、用户名、可选 schema 与编码。口令只以租户作用域的 secretRef 保存：`secretRef://tenant/<路径>` 按当前租户解析。platform / dev 引用一律拒绝，避免租户借平台凭据连接任意主机。
- `TestAsync` 先做出网检查（见下），再在使用点经宿主密钥策略（`ForHost(SecretPolicyId, 当前租户)`，默认策略 ID 为 qian.data）解析口令，交给方言探测，用后即释放。成功返回服务端版本与耗时；失败为结构化错误：
  - 目标不在允许的出网范围内：DATA_CONNECTION_TARGET_DENIED，此时既不连接也不读取密钥；
  - 网络不可达、域名解析失败或超时：DATA_CONNECTION_UNREACHABLE，可重试；
  - 服务端拒绝：DATA_CONNECTION_REJECTED，reason 为 authentication、database、schema、permission、parameter 或 server；
  - 密钥内容不能用作口令（如 PG 要求 UTF-8 文本）：DATA_CONNECTION_CREDENTIAL_INVALID；
  - 密钥无权解析或缺失：SECRET_* 错误。
- 超时由 `QianDataConnectionOptions.TestTimeout` 控制，含解析目标主机，1 秒到 1 分钟，默认 5 秒。
- 出网检查由 `QianDataConnectionOptions.Egress` 控制：
  - 目标主机解析出的每个地址都须通过，任一地址被拒即整体拒绝；连接只用检查过的地址，不再二次解析，并像驱动一样按顺序尝试，某个地址不可达才换下一个（如双栈主机先 ::1 后 127.0.0.1）。IP 字面量的变形写法（如 `127.1`、`0x7f.0.0.1`）按解析后的地址判定。
  - `BlockedNetworks` 一律拒绝，优先于放行网段。缺省含回环、链路本地（含云元数据 169.254.169.254）、100.100.100.200（阿里云元数据）、未指定地址、组播与广播。可追加集群内部网段（如 K3s 的 10.42.0.0/16、10.43.0.0/16）；本机开发可移除回环。
  - `AllowedNetworks` 非空时，只放行落在其中的地址，如租户数据库所在子网。
  - 网段写作 CIDR，写法无效时宿主启动即失败。`Egress.Permits(address)` 可供宿主复用同一判定。
- 详情与列表只返回 secretRef；任何响应、错误与审计记录都不含口令。

连通测试会从平台向档案中的主机出网（Agent 契约标 external）。上面的出网检查只是进程内的一道防线，完整的出口代理与 SSRF 基线按 PRD 7.19 归 S2；生产环境仍要用 NetworkPolicy 或出口代理限定可达网段；测试权限点单列为 qian.data:connection:test。

| 错误码 | HTTP | 含义 |
|---|---|---|
| DATA_TYPE_NOT_FOUND | 404 | 基准类型不存在 |
| DATA_TYPE_PROTECTED | 409 | 系统基准类型不可删除 |
| DATA_DIALECT_NOT_FOUND | 404 | 方言没有注册 |
| DATA_FIELD_DEFINITION_INVALID | 422 | 字段定义不满足方言约束，errors[] 为 DATA_FIELD_OUT_OF_RANGE、DATA_FIELD_FACET_UNSUPPORTED、DATA_FIELD_RANGE_INVERTED 或 VALUE_NOT_ALLOWED |
| DATA_CONNECTION_NOT_FOUND | 404 | 当前租户没有该连接 |
| DATA_CONNECTION_NAME_CONFLICT | 409 | 当前租户已有同名连接 |
| DATA_CONNECTION_UNREACHABLE | 422 | 网络不可达、域名解析失败或超时，可重试 |
| DATA_CONNECTION_REJECTED | 422 | 数据库拒绝连接 |
| DATA_CONNECTION_TARGET_DENIED | 422 | 连接目标不在允许的出网范围内，未连接 |
| DATA_CONNECTION_CREDENTIAL_INVALID | 422 | 密钥内容不能用作该数据库的口令，未连接 |

## 删除审计

QianSoftDeleteInterceptor 在 Kun 审计拦截器之前运行。AggregateRoot.Delete 和 DbContext.Remove 都落为软删除，填充 deleted_at / deleted_by；后者保留已有业务字段并让 Kun 同时更新 updated_at / updated_by。DeletedBy 与 CreatedBy 使用一致的可空用户键类型，没有 CLR 属性时用 shadow property，字段名由 QianDataConventions.DeletedByPropertyName 提供。匿名操作的删除主体为 null，已有删除审计不因普通更新而改写。

## 组件位置

本目录已有 DataPlugin 的实际入口和生命周期，契约放在 Qian.Data.Abstractions，业务组件只依赖 Definition。组件接受 dialect=pg、rls.enforce=true 和可选 connection 引用。带连接引用的 AddQianData 重载将上下文绑定到 platform/dev secretRef，凭据由宿主工厂在每次创建上下文时读取；组件的 connection 选择对应的上下文集合，未登记的引用或上下文会被拒绝。省略 connection 时沿用各上下文自身的配置，方便按组件使用独立 schema 和受限角色。租户挂载的 Data 只接受相同租户上下文，租户自带业务库仍在 S3 范围。

按已批准的 [S1-DEC-018](../../pm/tasks/S1-DEC-018.md)，数据本体先交付以解除注册表的实现前置。S1-RT-014 已回验第二阶段的 SDK 部分：清单的代码字段由源生成器派生（CI 以真实 Data 程序集 check；Qian.Data.Tests 按清单的 runtime.entry 装载真实 Provider，会话默认带租户填充、过滤与 RLS），包族经 main 上的 dotnet-release 工作流锁步发布到 GitHub Packages，发布与从该源真实还原的证据记在 S1-RT-014 日志。真实 `qian add data` 代码安装与 Provider 运行由 S1-RT-011 回验；通过前 `plugins/qian.data` 保留配置骨架标记，不宣称完整组件安装已经完成。

## 模型体验

无独立 Agent 工具。隔离装配失败使用 DATA_MISCONFIGURED，不输出连接串或凭据。Provider 的装配方法是宿主细节，业务代码通过 Data seam 使用数据能力。

## 已知限制与延期项

真实测试覆盖角色边界、自动迁移、空租户/跨租户写拒绝、数据约定、软删审计、同上下文 Outbox 原子性、不同上下文的缓冲隔离以及卸载释放。samples/Qian.Sample.Crm 已通过真实签名身份、HTTP 与 PostgreSQL 的千行双租户及原 SQL 索引验收。代码安装尚待 S1-RT-011 验收；S1-DEC-017 / S1-DEC-018 均已批准并落实到对应验收边界。

迁移协调、序号服务与雪花 ID 由 S1-DATA-002 交付，真实 PostgreSQL / Redis 测试覆盖两副本同时迁移、100 并发取号、规则修改与周期重置、三副本 WorkerId 互斥及正常停止和崩溃后的编号回收。它们目前以宿主装配（AddQianSequences、AddQianSnowflakeIds）提供，DataPlugin 还没有对应的服务键；业务组件经插件上下文取用要等组件安装验收一并补齐。其余组件的表还没有包内迁移，迁移任务对它们不执行 DDL。

基准类型目录、PG 方言与数据连接档案由 S1-DATA-003 交付，测试覆盖字段定义越界拒绝、系统类型删除保护、方言注册校验，以及真实 PostgreSQL 上的可达 / 端口关闭 / 错误口令 / 缺失库与 schema / 越权密钥，域名解析失败、对端不是 PostgreSQL、对端要求不支持的认证方式，租户隔离和详情不回传口令。它们同样以宿主装配（AddQianDataTypes、AddQianDataConnections、AddQianPgDialect）提供：方言的组件形态（向 `ctx.Data().Dialects` 注册、清单派生 `provides: Data.Dialects:pg`）要等装载器支持注册表键后补齐。不在本期：自定义基准类型、方言的 DDL 与分页、编辑未保存档案时的连通测试，以及用档案执行查询（S2 数据接口）。
