# Qian.Tenancy

租户主档目录与严格上下文传播 Provider；Definition 为 Qian.Tenancy.Abstractions。权威设计为 ADR-004、PRD §7.3，当前工作归 S1-TEN-001。

## 组件与表面

代码目录包含 Provider、租户入口边界、传播器、gRPC 适配器、主档上下文及内部持久化实现。TenancyPlugin 消费 Data、提供 Tenancy 服务键，目录挂在 global scope，供 Identity 等可信服务读取最小投影；不把联系方式、管理员、连接凭据或完整租户列表暴露给匿名调用方。物理 schema 遵循 Data 的插件命名规则，使用 plugin_qian_tenancy。

默认目标为 platform profile。当前尚未完成授权管理 API、真实 Identity 联调、私有包发布和 qian add tenancy，因此 plugins/qian.tenancy 保留 x-skeleton，不能视为已安装的完整平台组件。主档目录先提供稳定端口，避免 Identity 登录前查目录依赖尚未创建的 Membership。

## 主档存储

租户 UUID 由服务端生成，code 为 1 至 64 位小写 ASCII 字母数字；name 非空且最多 200 字符。当前只允许 row 隔离，新建状态为 pending；不会在创建目录记录时冒充已完成管理员开户或激活。code 与 isolation 不在编辑 DTO 中，EF 持久化后也拒绝变更。唯一索引处理跨宿主同号竞争，过期编辑由 Data/Kun 的 xmin 并发令牌拒绝。

主档是租户控制面全局目录，不承载业务行。内部写入实现尚未暴露为 HTTP 或运行时管理服务；后续运营入口须经过真实平台权限判定。tenant_admins 只保存 Membership UUID 指针，一租户一行，继承 Data 强制租户过滤与 RLS；Membership、密码和自然人内容归 Identity。管理员指针的开户编排属于 S1-TEN-003，不在目录写入时跨插件直写身份表。

## 上下文与传播

只复用 Kun.Core.MultiTenancy.ITenantContext，默认实现为已修复并正式发布的 Kun AsyncLocalTenantContext；Provider 不安装第二个上下文，也不改变宿主已有选择。

TenantRequestBoundary 在宿主完成令牌认证后使用 Kun 的 Production 解析器，只接受已认证身份中唯一的 tenant_id。Qian 再按冻结契约校验非空 UUID；缺失、重复、非法值、与现有范围冲突均返回 UNAUTHENTICATED，X-Tenant-Id 不参与解析。调用结束或异常时释放范围。平台运营主体没有租户，必须走独立的运营授权入口，不能由本边界回退为 default 租户。

TenantContextPropagator 从当前上下文写入 JSON 信封/作业参数或文本元数据。消费端显式传入从认证令牌或受保护调度记录获得的 authorizedTenantId，传播字段必须与之相同，且不得覆盖当前不同租户范围。该参数绝不能取自同一未受信消息。传播器本身不认证消息来源，不代替完整事件信封校验，也不允许缺失/null 平台消息进入租户业务入口；平台事件走独立通道。重复 tenant_id 保留并拒绝，不能先转字典而丢掉重复项。

TenantGrpcClientInterceptor 从当前上下文为四类 RPC 写入 tenant_id，不覆盖调用方已有的冲突或重复字段，也不修改可复用的原始 Metadata。TenantGrpcServerInterceptor 在宿主 JWT 验签后比较 claim 与元数据，再进入业务处理器；unary、服务端流、客户端流和双向流都使用同一边界。拒绝时返回 gRPC Unauthenticated，并在 qian-error-bin 二进制 trailer 中附带由 ErrorCatalog 生成的冻结 ErrorDetail JSON。宿主仍须配置认证和标准 HTTP 错误管道，适配器不自行签发令牌。

独立模式可以使用宿主实现的 ITenantContext，但必须支持可释放的 BeginScope；多租户入口没有 SingleTenant/default 隐式回退。

## 验证与剩余验收

使用共用 PostgreSQL 16 fixture 和受限应用角色验证真实目录写入、两宿主同号竞争、编辑冲突、租户过滤与原生 SQL RLS；通过真实 Data/Runtime/Tenancy 插件装载验证目录生命周期。HTTP 边界单元测试使用已认证 ClaimsPrincipal 测试宿主管道，不代表真实签名或管理员登录。JSON/元数据序列化往返验证通用传播逻辑；另外已用真实 Kestrel HTTP/2 与 gRPC 客户端验证四种 RPC、双租户并发、24 组非法入口和错误详情。测试令牌由临时 RSA 密钥签发并经 JwtBearer 完整验签，这是传输适配器集成测试，不代表已完成真实 Qian Identity 的签发/管理员登录验收。事件消费者和作业执行器尚未接通。

完整 TEN-001 验收仍需真实运营授权 API、Identity 管理员登录、事件/作业传播适配器和端到端 A/B 隔离。状态迁移、开户编排、租户设置分别归 TEN-002/003/004；不以目录基础实现关闭这些任务。

## 命令执行入口

TenancyPlugin 已在 SDK 的真实 command/dispatching 上登记租户策略，按已验证主体的租户 UUID 查询主档，只有 active/trial 可进入业务；pending/grace/suspended 和未知租户拒绝。此处不实施租户激活或到期调度，不代替尚未完成的生命周期和身份会话吊销。真实 CRM 命令已覆盖冻结拒绝及双租户并发。

能力目录可经 ITenantDirectory.GetByIdAsync 读取已验证主体的租户主档，复用命令策略的 active/trial 状态边界；该内部端口未增加匿名查询或管理写接口。
