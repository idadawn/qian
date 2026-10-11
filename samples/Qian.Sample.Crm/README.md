# 客户参考应用：数据入口

这是 qian.sample.crm 的数据基础，供 S1-DATA-001 验证真实 HTTP 列表、租户隔离和查询计划。独立 ASP.NET Core 宿主使用 Qian.Data，不依赖 Registry、Lifecycle 或 Capabilities。

## 启动

配置来自标准 IConfiguration；可用环境变量提供下列键：

- ConnectionStrings__Crm：受限 PostgreSQL 应用连接，不能使用超级用户或 BYPASSRLS。
- S2S__Issuer / S2S__Audience：受信任签发方和当前服务受众。
- S2S__ReaderClientId：允许读取客户的内部服务主体。
- S2S__PublicJwks：仅含 RSA/EC 公钥的 JWKS JSON，不放私钥。

```bash
dotnet run --project samples/Qian.Sample.Crm
```

库表由部署前迁移创建；应用启动只校验。CrmDbContext 使用 plugin_qian_sample_crm schema，客户继承 Qian 的安全上下文约定；分页索引是 tenant_id + id。

## 调用与授权

`GET /api/customers?skip=0&take=100` 返回 total 与 items；`GET /api/customers/{id}` 查询单个客户。服务令牌使用冻结 S2S claim 形状，principal_kind=service、sub 匹配 ReaderClientId、scope 含 crm.read、tenant_id 为有效租户 UUID。Kun 验证签名、issuer、audience 和有效期；生产租户解析只从已验证身份取得 tenant_id。

端点经过 Kun 权限判定端口。本应用的只读策略仅允许已配置服务主体对当前租户执行 qian.sample.crm:customer:read，不根据 roles 数组推导权限。返回错误统一使用 ErrorDetail，跨租户记录按不存在返回 404。

## 真实验收

Qian.Sample.Crm.Tests 启动真实 Kestrel 与共享 PostgresFixture，使用受限应用角色。管理员只建表并为 A/B 各造 1000 行；测试签发真实 RS256 令牌，遍历 A 的 1000 行、验证 B 隔离、越租户 404、伪造令牌/租户头/错误客户端和 scope 被拒绝。

测试捕获 HTTP 列表实际发出的 SQL 和参数，使用同一应用角色及租户会话执行 EXPLAIN ANALYZE，要求命中 ix_customers_tenant_id_id。不会关闭顺序扫描或扩大数据集来制造结果。CI 把实际计划保存在 test-results 的 evidence/crm-customer-list-explain.json。

## 范围

当前是只读数据入口。完整 CRM 的表单、附件、修订升级、审计展示和用户体验由 S1-QA-007 继续扩展；真实 Qian 身份服务的交互式登录由身份任务验收。本应用不会开放管理员造数接口或生成签名私钥。
