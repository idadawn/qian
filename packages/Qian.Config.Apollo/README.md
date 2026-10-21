# Qian.Config.Apollo

Apollo 原始 namespace、合并快照和离线缓存强制经过 `SecretSafety` 扫描；`ApolloConfigurationSchema` 可传入敏感属性规则，`ValidateApolloSnapshot` 只能追加校验。秘密通过 Config seam 的 [密钥 Provider](../Qian.Config.Secrets/README.md) 使用时解析，不能以明文写入 Apollo。

Config seam 的 Provider，复用当前 KunVersion 的 Kun.Configuration.Apollo 协议客户端。文件夹包含标准配置源装配、Apollo 引导解析、只读适配器与两个插件入口。标准模式只用 IConfiguration；Apollo 模式用于平台/服务配置，租户设置仍由 Tenancy 管理。

## 独立使用与源顺序

```csharp
var root = new ConfigurationBuilder().AddQianConfigurationSources(new QianConfigurationOptions
{
    BasePath = contentRoot,
    EnvironmentName = environmentName,
    SecretDirectory = mountedSecretDirectory,
    Arguments = args,
    ResolveAccessKey = reference => secretStore.Resolve(reference)
}).Build();
services.AddQianConfiguration(root);
services.Configure<SampleOptions>(root.GetSection("Sample"));
```

构建器必须为空，避免未知源悄悄改变优先级。顺序固定为 appsettings.json / appsettings.{EnvironmentName}.json → Apollo 公共 namespace → 插件 namespace → application → K8s Secret 文件 → 环境变量 → 命令行。公共/插件 namespace 各自保持声明顺序，application 固定最后。Secret 目录配置后必须存在，文件名中的双下划线遵循 KeyPerFile 的层次键规则。独立模式设 UseApollo=false 后仍保留其它标准配置源。

配置根由创建方释放；根释放时关闭文件监视与 Apollo 长轮询。宿主应使用同一根绑定 IOptionsMonitor 与只读 IConfigService。HTTP 宿主继续安装统一 Qian 错误处理。

## 引导项

引导只读取宿主文件、Secret 文件、环境变量和命令行，不读取 Apollo 自己返回的值。可以仅用以下环境变量启动：

| 键 | 约束 |
|---|---|
| Apollo__AppId | 必填应用标识 |
| Apollo__Env | 必填 UAT 或 PRO，用于校验和缓存隔离 |
| Apollo__ConfigServer__0 等 | 直连 ConfigService 的 HTTP(S) 地址，无内嵌凭据、查询参数和 fragment |
| Apollo__Cluster | 默认 default |
| Apollo__Namespaces__0 等 | 可选，默认 application；允许 qian.common.*、qian.plugin.*、application |
| Apollo__Label | 可选实例灰度标签 |
| Apollo__CacheDir | 可选缓存目录，下面按 UAT/PRO 隔离 |
| Apollo__AccessKey | 可选 platform/dev secretRef；有引用时必须提供解析回调 |

AccessKey 每次签名前解析；解析为空或服务返回 401/403 时，不用缓存伪装为首次加载成功。引用和引导值不进入 Apollo 快照；签名和协议缓存复用 Kun。默认客户端超时 70 秒，满足服务端 60 秒长轮询；自带客户端需由调用方管理生命周期及可信重定向。

## 插件装配与模型体验

ConfigurationPlugin 对应 qian.config，借用宿主标准配置根。ApolloConfigurationPlugin 对应 qian.config.apollo，要求根中确有本包安装的 Apollo 源，并核对 appId、env、meta 和 namespaces 声明；不匹配直接失败。两者只挂载于 global，提供 Config 服务键，经 ctx.Config() 读取。插件卸载撤销自己登记的服务和错误码，宿主配置根继续由宿主管理，不破坏独立消费者。

`ConfigurationPlugin` 与 `ApolloConfigurationPlugin` 分别带 `[QianPlugin]`，对应 `qian.config` 与 `qian.config.apollo`。同一程序集生成两份按 ID 隔离的元数据，分别同步各自唯一清单的 inject、provides、runtime 和 configSchema；两个 Apply 均直接登记 Config 服务，CI 检查漂移。程序集引用 Qian.Plugin.Sdk，生成器只参与编译。S1-RT-011 已从私有源 `qian add config` 安装并运行标准入口；Apollo 的清单已派生，完整安装门仍待验收。

只读配置端口面向受信任代码，不自动生成模型工具。涉及秘密解析权限、脱敏导出、Schema 治理与发布审计的接口仍由后续 CFG 任务实现。

## 运维边界

L0–L4 降级矩阵、排查顺序与证据见 [配置运维手册](../../docs/ops/configuration-runbook.md)。

## 验证与边界

快速测试复用 ApolloFixture 协议替身，覆盖五层顺序、namespace 顺序、环境引导、访问密钥引用和插件生命周期。最终验收使用 RealApolloFixture：真实 Apollo 2.5.2 ConfigService/AdminService 与 MySQL 8.0.43，通过 AdminService 编辑并发布，验证同一个 IOptionsMonitor 在 60 秒内更新、停服务后缓存可启动、无缓存失败。

这证明本机/CI 的真实服务链路。生产 Apollo 的高可用部署、Portal OIDC、配置审计及托管 MySQL 仍按部署与后续任务验收。

## 同程序集的两个组件

`ConfigurationPlugin` 对应 `qian.config`，`ApolloConfigurationPlugin` 对应 `qian.config.apollo`；每个 ID 各有一个入口和一份清单。二者只允许 global，均提供 `Config`，不会把两个入口的服务声明合并为虚构组件。前者仅接受空对象并借用宿主配置；后者逐项校验 appId / env / meta / namespaces 与宿主已有 Apollo 配置源一致，配置 Schema 来自 `ComponentConfiguration`。

`qian.config` 已通过既有真实安装门，移除骨架标记；Apollo 组件仍保留标记。新增代码元数据不改变配置源所有权或秘密解析边界。
