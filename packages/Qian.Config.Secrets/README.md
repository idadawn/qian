# 挂载式密钥解析

本目录是 Config seam 的密钥 Provider，Definition 在 `Qian.Config.Abstractions`。表面包含文件存储、公开引用解析、声明与审批策略、访问审计端口；不包含模型可调用的明文读取工具。默认随 `platform` 的 `qian.config` / `qian.config.apollo` 装配，也可在独立宿主使用。完整代码安装和私有 NuGet 锁步发布仍由 SDK/CLI 任务交付；当前不能声称 `qian add` 已完成代码安装。

## 引用与存储

公开引用遵循组件模型 v2：`secretRef://platform/pg/password`、`secretRef://tenant/t-001/db/token`、`secretRef://tenant/db/token`、`secretRef://dev/app/token`。租户相对引用按调用时真实 `ITenantContext` 解析，完整引用也必须匹配同一租户。`dev` 默认关闭，需开发宿主显式开启。

可信引导通过 `SecretBinding` 把公开引用映射到 `KubernetesSecretLocation`。低层兼容 `secretRef://k8s/namespace/name#key` 定位，文件布局为 `<MountRoot>/<namespace>/<Secret>/<key>`；该形式不能作为组件公开引用绕过权限。缺文件返回 `SECRET_NOT_FOUND`，无授权返回 `SECRET_ACCESS_DENIED`，读取或审计失败返回 `SECRET_UNAVAILABLE`，均不携带原值或物理路径。

每次使用重新打开文件，支持 Kubernetes 投射卷的 `..data` 原子符号链接轮换。不缓存密钥值，不使用 `subPath` 挂载。根目录不能自身为符号链接，所有解析后的目标必须留在受信任根内。根应由宿主只读挂载：这不是对拥有挂载写权限或同进程任意代码执行能力的隔离沙箱。单文件最大 1 MiB，空文件拒绝；二进制凭据可使用字节接口。

## 权限与生命周期

`policy.permissions.secrets` 的条目是 `{ "name": "oss/*", "scope": "platform" }`。`name` 支持精确路径或末尾 `/*` 前缀，`scope` 为 platform、tenant、dev。可信宿主分别提供从清单取得的 `Declared` 和审批记录中的 `Approved`，两者取交集，声明本身不授予权限。tenant 的匹配路径不含租户 id；租户身份另外检查，不能以通配符跨租户。

`AddQianSecrets(options)` 登记工厂，宿主必须显式登记 `ISecretAccessObserver`。没有审计接收端时不能解析；写入失败时不返回已读值。访问记录只含调用方、规范引用、租户、结果；生产接收端由审计 Provider 持久化，不以空日志器冒充审计链。解析前后及审计完成后均检查插件仍活动、Config 服务仍可见、租户未变化。缓存的解析器不能跨插件卸载继续使用。

插件使用 `ctx.ResolveSecretAsync(reference)`；独立可信宿主可用工厂 `ForHost(policyId, tenantContext)`。工厂、策略和文件存储均属可信宿主 API，不能绑定为 HTTP/模型入口，不能让请求选择 policyId。原生同进程插件本身不是安全沙箱；受限插件须经平台执行边界调用。策略与绑定在工厂构造时复制，审批变更需重新装配工厂；不从运行中请求扩大授权。

`SecretValue` 默认打印脱敏，JSON 序列化拒绝。使用 `using` 及时释放；对象自有字节缓冲会归零。`RevealUtf8()` 是给真实客户端调用的显式出口，调用方创建的字符串无法保证清零，禁止写入配置导出、日志、事件、模型消息或数据库。异步消费者必须在使用结束后再释放值。

## 配置扫描

`SecretSafety` 同时用于 Apollo 候选快照与 [CI 工具](../../tools/Qian.Secrets.Tool/README.md)。Schema 的 `x-qian-sensitive: true`（兼容旧 `sensitive: true`）只接受合法公开引用。支持本地 `$ref`、属性、模式属性、数组和组合/条件分支；各分支敏感标记保守取并集。外部或无法解释的 Schema 引用报错，不静默跳过。它不是完整 JSON Schema 验证器。

模式属性的键按 ECMA-262 方言解释，即 JSON Schema 2020-12 的规定：`\d`、`\w`、`\s` 只认 ASCII。判定与配置键一样不区分大小写：与键名相等（OrdinalIgnoreCase）的任一写法能匹配就算匹配，所以 `Endpoints:Legacy_billing` 同样落在 `^legacy_` 下。只接受各方言含义相同的写法，规则见 [共享内部源文件](../Common/README.md)。词边界、内联选项、Unicode 属性、环视、反向引用等不支持的写法，以及语法错误、过大的自动机和匹配超时，都报 `CONFIG_SECRET_SCHEMA_INVALID`，不会跳过敏感标记。

键名含各方言判断不同的字符（换行、只有 ECMA-262 算作空白的字符、U+0130、代理项），且所在对象声明了模式属性时，不按模式取舍，整体按敏感处理，只接受合法引用。匹配用 .NET NonBacktracking 引擎，线性时间；同一模式在进程内复用编译实例，最多缓存 128 个，超出时清空重建，失败的模式不进缓存。单次匹配超时 1 秒。

即使没有 Schema，敏感键名、JWT/私钥形状、含凭据的 URL/连接字符串、疑似高熵值也会拒绝；诊断仅含规则与位置。启发式扫描不能证明任意字符串都不含秘密，插件仍须正确标注敏感属性。普通参数、无凭据 URL、规范引用和带 `sha256:` 前缀的摘要可以通过。Apollo 的默认检查不能由额外宿主校验回调关闭；K8s Secret 文件作为高优先级秘密来源，不纳入 Apollo 非敏感快照。

配置分工见 [配置开发与运行手册](../../docs/ops/configuration-runbook.md)。本任务不替代后续审计链、审批持久化、Schema 自动收集或完整 platform 的联调验收。
