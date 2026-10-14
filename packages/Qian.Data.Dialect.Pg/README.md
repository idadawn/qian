# Qian.Data.Dialect.Pg

PostgreSQL 方言组件（组件 id `qian.data.dialect.pg`），实现 Data seam 的方言 SPI qian.data.dialect/v1。契约在 [Qian.Data.Abstractions](../Qian.Data.Abstractions/README.md)，目录与连接档案在 [Qian.Data](../Qian.Data/README.md)，任务见 [S1-DATA-003](../../pm/tasks/S1-DATA-003.md)。

## 宿主装配

```csharp
services.AddQianPgDialect();
services.AddQianDataTypes();          // 只要类型目录
services.AddQianDataConnections(...); // 或连同数据连接档案
```

`AddQianPgDialect` 把 `PgDialect` 作为 `IDataDialect` 登记，注册表以描述数据的代码 `pg` 注册（`Data.Dialects:pg`）。

## 方言数据

[pg-dialect.json](pg-dialect.json) 随包嵌入，加载时用 `DataTypeContracts.ReadDialect` 按系统基准类型核对，结构见 data-types.schema.json 的 `$defs/dbDialect`。改映射只改这份数据。

| 基准类型 | 原生类型 | 约束 |
|---|---|---|
| int | integer | 取值 −2147483648 到 2147483647，整数 |
| bigint | bigint | 取值为有符号 64 位，整数 |
| decimal | numeric | 精度 1 到 28（缺省 18），小数位 0 到精度（缺省 2）；上下限由精度与小数位推出 |
| float | double precision | 不另设上下限 |
| datetime | timestamp with time zone | 精度（秒的小数位）0 到 6，缺省 6 |
| varchar | character varying | 长度 1 到 10485760，缺省 255 |
| text | text | 无 |
| bool | boolean | 无 |
| json | jsonb | 无 |
| uuid | uuid | 无 |

decimal 的精度上限取 28 位，是 .NET decimal 能无损往返的位数；PostgreSQL 的 numeric 本身支持更大的精度。

## 连通探测

`ProbeAsync` 用 `NpgsqlConnectionStringBuilder` 设置主机、端口、库、用户名、口令、search_path 与 client_encoding，不拼接字符串：

- 主机由平台给出，已是通过出网检查的 IP 地址，方言不再解析档案里的主机名（见 [Qian.Data](../Qian.Data/README.md) 的出网检查）；
- 探测连接不进连接池，用完即关，应用名为 `qian.data connection test`；
- 连接与命令超时取调用方给的超时，Npgsql 上限为 1024 秒；
- 给了 schema 时，再查 `has_schema_privilege(…, 'USAGE')`：schema 不存在为 reason=schema，没有 USAGE 权限为 reason=permission。

错误映射：

- 密钥内容不是 UTF-8 文本 → DATA_CONNECTION_CREDENTIAL_INVALID，不发起连接；
- SQLSTATE 28P01 / 28000 → DATA_CONNECTION_REJECTED，reason=authentication；
- 3D000 → reason=database；
- 42501 → reason=permission；
- 22023 → reason=parameter；
- 服务端要求驱动不支持的认证方式（如 Kerberos V5）→ reason=authentication；
- 08、53、57 类以及没有服务端错误码的驱动异常（DNS、拒绝连接、TLS、协议）和超时 → DATA_CONNECTION_UNREACHABLE；对端不是 PostgreSQL（例如端口上跑的是 HTTP 服务）也归入此类；
- 其余服务端错误 → reason=server。

错误不携带口令或驱动原文，原始异常只作为内部异常留给服务端日志。

## 已知限制

不提供 DDL 与分页方言（S2 数据接口与建模交付），SSL 模式取 Npgsql 缺省值。组件形态（作为插件向 `ctx.Data().Dialects` 注册、清单派生 `provides`）要等装载器支持注册表键；MySQL 与国产库方言本期不做。
