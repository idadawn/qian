# Qian.Data.AspNetCore

qian.data 的 HTTP 表面：基准类型目录（全局元数据）与数据连接档案（按租户）。服务实现与装配见 [Qian.Data](../Qian.Data/README.md)，任务见 [S1-DATA-003](../../pm/tasks/S1-DATA-003.md)。

## 宿主装配

```csharp
builder.Services.AddQianErrors();
builder.Services.AddKunAuthorization();            // 另需登记 IPermissionChecker（如 Qian.Authz）
builder.Services.AddQianPgDialect();
builder.Services.AddQianDataConnections((sp, options) => options.UseNpgsql(appConnection));
var app = builder.Build();
app.UseQianErrors();
app.UseAuthentication();
app.UseKunMultiTenancy();
app.UseAuthorization();
app.MapQianData();                                  // 或分别 MapQianDataTypes / MapQianDataConnections
```

平台模式传入 `PluginEndpointBinding`，端点还要求该组件条目在当前 scope 已挂载（未挂载一律 404）。

## 端点

| 方法与路径 | 权限点 | 说明 |
|---|---|---|
| GET /api/data/meta/types | qian.data:type:read | 基准类型、方言与映射，结构见 data-types.schema.json |
| DELETE /api/data/meta/types/{code} | qian.data:type:delete | 系统类型返回 409 DATA_TYPE_PROTECTED，未知类型 404 |
| GET /api/data/connections | qian.data:connection:read | 当前租户的档案列表 |
| POST /api/data/connections | qian.data:connection:write | 新建，201 并带 Location |
| GET /api/data/connections/{id} | qian.data:connection:read | 详情，不含口令 |
| PUT /api/data/connections/{id} | qian.data:connection:write | 整体替换 |
| DELETE /api/data/connections/{id} | qian.data:connection:write | 软删，204 |
| POST /api/data/connections/{id}:test | qian.data:connection:test | 连通测试，成功返回服务端版本与耗时 |

- 所有响应带 `Cache-Control: no-store`，错误统一为 ErrorDetail。
- 数据连接端点要求已验证租户，没有租户的主体返回 403 AUTHZ_DENIED。
- 请求体先按成员白名单检查：password 等未声明成员报 UNKNOWN_PROPERTY，缺少必填成员报 REQUIRED，类型不符报 TYPE_MISMATCH；再由服务校验格式、方言与 secretRef，逐项返回 INPUT_INVALID 的 errors[]。请求体上限 16 KiB。
- 权限点与默认角色模板声明在 plugins/qian.data/metadata。

## 已知限制

不提供编辑未保存档案时的连通测试。连通测试会从平台出网，生产环境要用 NetworkPolicy 或出口代理限定可达网段。
