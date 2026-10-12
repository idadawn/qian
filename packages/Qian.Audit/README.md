# Qian.Audit

审计 Provider 的存储基础，当前对应 S1-AUD-001。权威设计为 ADR-009 与 PRD §7.11.1，Definition 位于 Qian.Audit.Abstractions。

## 组件与交付边界

本目录包含确定性内容哈希、AuditDbContext 与内部 PostgreSQL 链存储。目标是在每个 .NET profile 中经 qian.audit 提供 Audit seam，并随 SDK 默认装配到实际能力执行链。当前尚未提供公共 IAuditWriter、运行时 waterfall 监听器或安装入口；plugins/qian.audit 保留骨架，不能以本次存储测试宣称 qian add audit、完整 platform 或 CRM 业务审计已经完成。

## 记录与内容哈希

AuditRecord 保留租户、actor_type、actor_id、on_behalf_of、capability_id、contract_version、input_hash、channel、resource、result、trace_id、occurred_at 和补充 payload。记录不可变且字符串化不展开主体或 payload。input_hash 由 AuditInputPolicy 在 Schema 脱敏后计算；实际能力管道仍须消费这个策略，不能把原始输入直接塞入 payload。

v1 内容哈希为 SHA256(prev_hash 的 UTF-8 文本拼接 canonical_json(entry))。哈希输入是记录的 snake_case 字段加 seq、format_version，包含全部补充数据；prev_hash 在 JSON 之前作为前缀，首条为空字符串。键按 ordinal 排序，数组顺序保留，重复键拒绝。数值按精确十进制规整，不经 double 丢失大整数；指数限定在正负 1000 内、有效数字最多 1024 位、小数位最多 16383 位。字符串使用 System.Text.Json 默认转义。发生时间先统一 UTC 并规整到 PostgreSQL 微秒精度，避免保存后无法重放原哈希。固定向量另由 Python 独立计算验证。

## 输入脱敏策略

AuditInputPolicy 从可信能力契约复制 JSON Schema 2020-12；schema、isCommand、audit 都不能来自调用请求。命令默认记录、查询默认不记录，可信契约可以显式覆盖。关闭时 InputHash 返回 null，不产生伪造的输入哈希；调用执行器仍须负责在此情况下不写审计记录。

sensitive 和 x-qian-sensitive 的 true 标记统一遮盖为固定字符串 [REDACTED]，整个对象或数组被标记时连同内部结构一起遮盖。对象属性、本地 JSON Pointer 引用、数组位置/元素、patternProperties、additionalProperties 与条件组合均参与传播；组合或条件的任一候选分支标敏感就遮盖，false 不能抵消另一分支的 true。该策略可能多遮盖非生效分支的值，避免分支切换导致秘密进入哈希。未声明字段的值也保守遮盖；属性名本身标敏感时遮盖整个对象。

先验证输入结构与重复键，再生成脱敏投影并按原 Schema 验证输入，最后只对投影的规范化 JSON 计算 SHA-256。记录层只接收 input_hash；补充 payload 仍须来自可信审计元数据，不把脱敏投影或原输入自动复制到 payload。策略不修改原输入，Schema 文档释放后仍可并发使用。

只接受离线本地 Pointer 引用；外部引用、同层引用循环、未支持的动态引用/anchor、嵌套 id、无效标记与不能安全执行的动态键模式均返回 AUDIT_SCHEMA_INVALID。有限深度的递归对象引用可用。不会联网补齐未知 Schema，也不会在无法解析时改为计算原输入哈希。错误信息不带 Schema/输入原值；字段脱敏策略不代替业务验证或身份授权。

## 存储与并发

AuditDbContext 继承 QianDbContext，拥有 plugin_qian_audit schema。audit_entries 具有事件 ID、内容哈希、链与序号、链与前序的唯一约束。audit_chain_state 保存 tip_seq 和 tip_hash。租户列按 Data 强制过滤与 RLS 保护，受限应用角色执行在线 DML；平台 null 行虽可表示，当前普通入口一律拒绝无租户，不借 null 自动获得平台权限。

追加在取得租户 session advisory lock 后开启 SERIALIZABLE 事务，再写记录与推进链头。锁在快照创建前取得，避免排队事务持有旧快照而持续冲突；序列化或死锁错误有界重试。取消和失败都会释放锁；清理失败时清理对应连接池，避免复用可能仍持锁的连接。事件 ID 重试仅在完整内容一致时幂等，不同内容返回 CONFLICT；损坏链头不继续追加。

分页读取和链验证是内部存储端口，尚未作为无授权查询 API 对外开放。验证用 REPEATABLE READ 快照和分批读取，避免并发追加产生误报；报告最早缺号、前序、内容或链头不一致的位置。补充 payload 与委托主体都参与哈希，修改历史值会被检测。

## 验证与剩余任务

真实 PostgreSQL 16、受限角色、两独立宿主验证千条并发追加、连续序号/前序、同事件幂等、委托主体分页查询、RLS 隔离、写入失败整笔回滚以及历史篡改定位。测试通过并不意味着参考应用同事务要求已经完成：当前内部 AppendAsync 的独立事务只包含审计记录与链头，后续必须与真正的 CRM 业务事务、Outbox 及能力生命周期连接，不能把独立追加成功当成业务回滚验证。

尚未完成：受保护平台链、真实能力/属性对脱敏策略的消费、真实 Mediator 与 waterfall 装配、卸载撤销、CRM 同事务、私有包发布和 qian add 安装。未引入事后不同事务的旧审计拦截器。
