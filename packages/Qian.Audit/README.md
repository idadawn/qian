# Qian.Audit

审计 Provider 与存储基础，当前对应 S1-AUD-001。权威设计为 ADR-009 与 PRD §7.11.1，Definition 位于 Qian.Audit.Abstractions。

## 组件与交付边界

本目录包含 AuditPlugin、确定性内容哈希、输入脱敏与 PostgreSQL 链存储。AuditPlugin 消费 Data 并提供 Audit 服务键，ctx.Audit() 返回 IAuditService（IAuditWriter + IAuditReader）。可挂在 global 或 tenant scope，默认兼容 chain.store: pg。目标是在每个 .NET profile 中随 SDK 装配到实际能力执行链；完整组合包与安装入口仍待完成，因此 plugins/qian.audit 保留骨架，不能据此宣称 qian add audit 或完整 platform 已经完成。

## 记录与内容哈希

AuditRecord 保留租户、actor_type、actor_id、on_behalf_of、capability_id、contract_version、input_hash、channel、resource、result、trace_id、occurred_at 和补充 payload。记录不可变且字符串化不展开主体或 payload。input_hash 由 AuditInputPolicy 在 Schema 脱敏后计算；实际 SDK 命令监听器消费这个策略，不能把原始输入直接塞入 payload。

v1 内容哈希为 SHA256(prev_hash 的 UTF-8 文本拼接 canonical_json(entry))。哈希输入是记录的 snake_case 字段加 seq、format_version，包含全部补充数据；prev_hash 在 JSON 之前作为前缀，首条为空字符串。键按 ordinal 排序，数组顺序保留，重复键拒绝。数值按精确十进制规整，不经 double 丢失大整数；指数限定在正负 1000 内、有效数字最多 1024 位、小数位最多 16383 位。字符串使用 System.Text.Json 默认转义。发生时间先统一 UTC 并规整到 PostgreSQL 微秒精度，避免保存后无法重放原哈希。固定向量另由 Python 独立计算验证。

## 输入脱敏策略

AuditInputPolicy 从可信能力契约复制 JSON Schema 2020-12；schema、isCommand、audit 都不能来自调用请求。命令默认记录、查询默认不记录，可信契约可以显式覆盖。关闭时 InputHash 返回 null，不产生伪造的输入哈希；调用执行器仍须负责在此情况下不写审计记录。

sensitive 和 x-qian-sensitive 的 true 标记统一遮盖为固定字符串 [REDACTED]，整个对象或数组被标记时连同内部结构一起遮盖。对象属性、本地 JSON Pointer 引用、数组位置/元素、patternProperties、additionalProperties 与条件组合均参与传播；组合或条件的任一候选分支标敏感就遮盖，false 不能抵消另一分支的 true。该策略可能多遮盖非生效分支的值，避免分支切换导致秘密进入哈希。未声明字段的值也保守遮盖；属性名本身标敏感时遮盖整个对象。

先验证输入结构与重复键，再生成脱敏投影并按原 Schema 验证输入，最后只对投影的规范化 JSON 计算 SHA-256。记录层只接收 input_hash；补充 payload 仍须来自可信审计元数据，不把脱敏投影或原输入自动复制到 payload。策略不修改原输入，Schema 文档释放后仍可并发使用。

只接受离线本地 Pointer 引用；外部引用、同层引用循环、未支持的动态引用/anchor、嵌套 id、无效标记与不能安全执行的动态键模式均返回 AUDIT_SCHEMA_INVALID。有限深度的递归对象引用可用。不会联网补齐未知 Schema，也不会在无法解析时改为计算原输入哈希。错误信息不带 Schema/输入原值；字段脱敏策略不代替业务验证或身份授权。

动态键模式（patternProperties 的键）按 ECMA-262 不带标志的语义解释，即 JSON Schema 2020-12 核心规范 §6.4 的规定：`\d`、`\w` 只认 ASCII，`\s` 为 ECMA-262 的空白与行终止符。只接受各方言含义相同的写法：字面字符，字符类、范围与否定类，`\d \D \w \W \s \S`，`.`、`^`、`$`，分组 `(…)` 与 `(?:…)`，`|`，量词 `* + ? {n} {n,} {n,m}` 及其惰性形式，以及 `\t \n \r \f \v`、`\xHH`、`\uHHHH` 和语法字符的转义。词边界、内联选项、Unicode 属性、命名组、环视、反向引用、类减法、空类、未转义的 `{ } ]`、代理项、否定类里的否定简写、未配对的分组、倒置的量词边界，以及展开量词后超过 1000 个字符与锚点的写法，都在构造策略时返回 AUDIT_SCHEMA_INVALID。

各方言对少数字符判断不同：换行（影响 `$`），回车与 U+2028/2029（影响 `.`），非 ASCII 空白（影响 `\s`），U+0130（验证器的 `\w` 包含它），代理项（带 u 标志时按码点计数）。键名含这些字符、且对象声明了 patternProperties 时，该键的值整体遮盖，不按模式取舍。

翻译与匹配和配置扫描、profile 装配共用 [共享内部源文件](../Common/README.md) 的实现；审计面对的是区分大小写的 JSON，不折叠大小写。模式翻译后交给 .NET NonBacktracking 引擎做线性时间匹配，过大的自动机在构造时即拒绝；同一模式在进程内复用编译实例，最多缓存 128 个，超出时清空重建。单次匹配超时 1 秒，超时同样返回 AUDIT_SCHEMA_INVALID，不会跳过敏感标记。

## 存储与并发

AuditDbContext 继承 QianDbContext，拥有 plugin_qian_audit schema。audit_entries 具有事件 ID、内容哈希、链与序号、链与前序的唯一约束。audit_chain_state 保存 tip_seq 和 tip_hash。租户列按 Data 强制过滤与 RLS 保护，受限应用角色执行在线 DML。普通入口一律要求当前租户，记录的租户必须与上下文一致。平台 null 记录只通过下述独立入口访问，不以缺少租户自动切换模式。

追加在取得租户 session advisory lock 后开启 SERIALIZABLE 事务，再写记录与推进链头。锁在快照创建前取得，避免排队事务持有旧快照而持续冲突；序列化或死锁错误有界重试。取消和失败都会释放锁；清理失败时清理对应连接池，避免复用可能仍持锁的连接。事件 ID 重试仅在完整内容一致时幂等，不同内容返回 CONFLICT；损坏链头不继续追加。

分页读取和链验证是内部存储端口，尚未作为无授权查询 API 对外开放。验证用 REPEATABLE READ 快照和分批读取，避免并发追加产生误报；报告最早缺号、前序、内容或链头不一致的位置。补充 payload 与委托主体都参与哈希，修改历史值会被检测。

## 加入业务事务

AppendEnlistedAsync 接收调用方已有的 PostgreSQL SERIALIZABLE 事务，核验受限登录角色、当前租户与连接的 app.current_tenant_id 一致后，把记录和链头写入这笔事务；它不提交、不释放、不局部重试业务事务。返回节点在外层提交前尚未持久化。调用方遇到任何异常须回滚；序列化冲突需释放整个 Data 会话，从新会话重试包含业务写入的完整操作。平台链另有同一约束的同事务入口 AppendPlatformEnlistedAsync，见下文「受控平台链与生命周期」。

迁移时先创建独立受限 NOLOGIN、NOINHERIT 函数所有者，再在同一迁移事务调用 AuditTransactionAccess.CreateMigrationSql(writerRole, producerRole)。Audit 自有的 SECURITY DEFINER 函数使用固定 search_path、完整限定表名及原有强制租户 RLS；业务登录只获函数执行权，不获审计表直接访问权限。函数所有者无角色成员关系、无平台旁路，仅能追加记录与推进链头，不能更新、删除或清空历史记录。业务 schema 的授权仍由业务组件负责。

事务追加与独立追加使用同一租户 advisory lock 键；前者持有事务锁直到提交或回滚。函数检查当前事务隔离级别、租户、节点序号、前序及传入正文的摘要；C# 写入器生成规范化正文并重放已有链头与幂等记录的哈希。这是给可信组件的数据库端口，不能把函数直接作为客户端 API：持有执行凭据的生产方负责记录来源和规范化正文，数据库不独立重做全部 .NET JSON 规范化规则。

真实 CRM 模型验证客户与审计同时可见、显式回滚无残留、审计写入失败使业务事务回滚；双宿主强制制造旧快照冲突后重试完整业务，客户与审计仍一一对应。参考应用现已通过真实 HTTP、生成的 Capability 契约、生产执行 waterfall 和实际 RBAC；客户、回执、Outbox 与审计同时提交，审计失败时全部回滚。当前审计能力 ID 使用冻结 R23 的 component/name 稳定形式：qian.sample.crm/customer.create。

## 命令监听器

AuditPlugin 在 SDK 的 command/dispatching Definition 上登记生产 waterfall 监听器。它读取生成契约的 sideEffect/audit、从真实输入 Schema 计算脱敏哈希，调用实际 handler 与 Data 保存后，使用同一 DbTransaction 追加审计。审计失败向外传播，事务由 SDK 回滚；查询默认不记录，显式 audit 开启时适用同一流程。已验证创建命令，以及真实列表默认不审计、详情显式 audit 后入链。

卸载组件时，该监听器随效果栈按登记的逆序撤销（PRD 7.11.1、ADR-020 D20.2）。真实登记顺序依次为错误码、审计服务释放、审计服务、策略租约释放、waterfall 监听器、策略租约；撤销严格反过来，监听器在策略租约之后、审计服务注册与服务释放之前撤下，错误码最后撤销。卸载后链上只剩其它组件的监听器，审计服务与策略租约都不可解析，旧服务句柄已释放；重装得到新的单个监听器。`AuditListenerLifecycleTests` 用真实 AuditPlugin 与 PluginLoader 验证，记录器只原样转发上下文，并在每次登记后压入标记 effect 以读出撤销顺序。

记录包含真实主体种类、主体 ID、生成能力 ID、契约版本、通道和链路 trace_id。S1 不签发的 act 委托入口明确拒绝，不将未知委托静默记为普通操作；直接存储端口的 Agent/on_behalf_of 预留验证继续保留。等待真实链锁时卸载 Audit，会取消在途命令并回滚已保存的客户、回执和 Outbox；依赖入口随后不可用。

## 受控平台链与生命周期

平台链默认关闭。启用时，宿主先独立创建受限的 NOINHERIT LOGIN 角色，禁止 SUPERUSER、BYPASSRLS、CREATEDB、CREATEROLE、REPLICATION 和任何角色成员关系。凭据由宿主或 Secret 服务持有，不进入组件配置。迁移在普通 Audit 模型建表后，同一迁移事务中应用 AuditPlatformAccess.CreateMigrationSql(role)，只修改 Audit 自有两张表的策略和该角色授权。

宿主用 AddQianData 分别绑定 AuditDbContext 的租户凭据与 PlatformAuditDbContext 的平台凭据，再在 global AuditPlugin 配置 chain.platformRole。启动验证两条连接不共用登录身份；每次平台调用检查当前角色、登录角色、角色属性及必要权限。平台角色只能读取/追加平台记录与推进链头，无权更新、删除或清空历史记录。RLS 同时检查 current_user、session_user 和空租户 GUC，因此普通角色借 SET ROLE 不能冒充平台登录，平台角色设置租户 GUC 也不能读写租户数据。

AppendPlatformAsync、AppendPlatformEnlistedAsync、ReadPlatformAsync、VerifyPlatformAsync 是显式入口，另外要求传入仍有效的 global 生产方 IPluginContext，且该上下文解析到当前同一个 Audit 服务。tenant 生产方即使重新 Scoped 到 global 也被拒绝；存在租户上下文时拒绝平台操作。此生产方许可只用于记录/读取审计，不能替代 Authz 对实际业务操作的授权。端口供可信组件使用，不提供客户端直接写审计的 HTTP 能力；面向用户的读取须另行鉴权并审计。

平台链的同事务入口（S1-AUD-009）：AppendPlatformEnlistedAsync(producer, record, transaction) 把平台记录挂进调用方已有的 PostgreSQL SERIALIZABLE 事务，与 AppendEnlistedAsync 同一约束——不提交、不释放、不局部重试，返回节点在外层提交后才持久化；调用方遇到任何异常须回滚，序列化冲突须释放整个 Data 会话、从新会话重试包含业务写入的完整操作。平台级状态变更（如 Lifecycle 的安装、激活）因此与平台链条目同一事务提交，业务失败即无审计，不再有独立追加之后、业务提交之前的孤儿条目窗口。生产方检查与 AppendPlatformAsync 相同（服务与生产方都是 global、同一个 Audit 实例、已配置 chain.platformRole），存在租户上下文或记录带租户时返回 AUTHZ_DENIED，非 Npgsql 或非 SERIALIZABLE 事务返回 INPUT_INVALID。写入器还先核对连接：受限登录（非 superuser / BYPASSRLS）、没有 SET ROLE、不是平台登录、没有租户 GUC，且两个平台函数的执行权直接授予该登录；不满足时同样返回 AUTHZ_DENIED，宿主漏了下述迁移时由此可诊断，而不是在函数调用处报数据库权限错误。

启用这个入口时，迁移改用 AuditPlatformAccess.CreateMigrationSql(platformRole, writerRole, producerRole)，同样在租户守卫之后、同一迁移事务应用：在一参重载的策略与授权之外，新建 SECURITY DEFINER 函数 platform_tip / platform_append，属主 writerRole 与租户链函数是同一个独立受限 NOLOGIN 角色，producerRole 只获执行权，多个生产方各执行一次。平台登录的 RLS 分支一字不改；新增的属主分支只在 tenant_id 为空、无租户 GUC、current_user 是属主、session_user 不是平台登录且属主没有任何成员关系时放行平台行。属主不能登录，非超管无法 SET ROLE 成它，成为它的唯一路径是它拥有的函数；属主一旦被授予成员关系，这条分支与两个函数都随之拒绝，迁移也拒绝重跑。函数自身再核对：事务为 SERIALIZABLE 且无租户 GUC；调用方是受限登录、不是平台登录，且执行权直接授予该登录本身，借成员关系继承执行权或 SET ROLE 成生产方的其它登录都被拒绝；正文不带租户；摘要、序号与前序接得上链头。platform_tip 只返回序号与哈希（下一个序号与链头哈希；同一事件 ID 已在链上时返回它的序号、前序与内容哈希，供写入器判断幂等），不把平台记录内容交给生产方；链头一致性由函数核对，不一致时写入器返回 CONFLICT，整条平台链的读取与验链仍只经平台登录。同事务入口与独立追加共用 qian.audit:platform 锁键与同一条平台链，持旧快照的后到者以可串行化失败交给调用方重做。

因此原有保证都不变：应用角色直接读写平台记录仍被 RLS 拒绝；平台登录不能更新、删除或清空历史，设置租户 GUC 也读不到租户行；普通角色借 SET ROLE 既不能冒充平台登录，也不能冒充函数属主。平台审计登录仍然必需：配置 chain.platformRole 时挂载要校验它，平台链的读取与验链、Authz 超管旁路等其它平台生产方的独立追加都经它。原生 Provider（Qian.Audit.Native）暂不提供这个入口，调用时抛 NotSupportedException。

服务卸载先拒绝新调用、取消并排空进行中的数据库调用，再撤销错误码。等待数据库锁的追加也会取消并释放连接；旧缓存句柄在卸载后失效，重装取得新服务。tenant scope 的服务额外检查调用上下文与其挂载租户一致。

## 验证与剩余任务

真实 PostgreSQL 16、受限角色、两独立宿主验证千条并发追加、连续序号/前序、同事件幂等、委托主体分页查询、RLS 隔离、写入失败整笔回滚以及历史篡改定位。存储测试与参考应用执行测试分别验证其边界：AppendAsync 的独立事务只包含审计记录与链头；平台链同事务入口（AuditPlatformTransactionTests）覆盖与业务写入同提交同回滚、与独立追加共用一条链、幂等与冲突、损坏链头、生产方与连接检查、函数的调用方检查、RLS 对应用角色与借成员关系切换为属主的拒绝，以及持旧快照的可串行化失败；AppendEnlistedAsync 已覆盖真实 CRM 数据上下文回滚，已与实际命令执行和业务 Outbox 连接，跨进程审计事件发布及实体审计接线仍待完成，不能把独立追加成功当成业务回滚验证。

尚未完成：实体审计与跨进程审计事件、完整默认组合包和 qian add 安装（包已随 0.1.0 发到 GitHub Packages，R93）。未引入事后不同事务的旧审计拦截器。

RT-009 的已授权 hook 调用使用 hook 审计通道；SDK 可信适配器与同事务 SQL 校验同步支持该通道。普通请求不会继承上次 hook 标记，hook 查询按事件执行要求强制审计。

## 清单入口与代码派生

`plugins/qian.audit/.qian-plugin/plugin.json` 的稳定入口是 `AuditComponent`，允许 global / tenant scope，依赖 `Data`，提供 `Audit` 与 `CommandAudit`。固定委托无参 `AuditPlugin`；宿主仍准备受限数据上下文与可选的平台审计登录。

入口的 `Inject`、独立服务声明与 `ComponentConfiguration.Schema` 是清单代码字段及配置 Schema 的来源；通过 `qian-plugin sync` 更新组件目录，再用 `check` 验证漂移。元数据补齐不替代真实安装验收，本组件继续保留 `x-skeleton`。
