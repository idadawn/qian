# 特性开关草稿管理组件

`qian.config.flags` 是可选平台管理组件，Definition 在 `Qian.Config.Abstractions`，Provider 与独立入口在 `Qian.Config.Apollo`。文件夹包含唯一清单、配置 Schema、`flags.set` 工具元数据与 `flag:set` 权限声明。仅允许 global scope、platform profile，消费 Config、AuthzEnforcement、Audit、NativeData，提供 FeatureFlagDrafts。

读者先建立并发布 `qian.common.flags` 公共 namespace，并在所选宿主的可信 Apollo 引导与配置组件条目中显式订阅；跨应用还须关联公共 namespace。默认 profile 不自动增加订阅，没有规则时为 false。

组件配置只有 `bindingId`，由可信宿主预先登记固定 Portal 地址、应用、环境、集群、操作员、平台 Token 引用和回执数据库绑定。工具只接受开关名称、固定规则、确认输入与幂等键；只编辑 `qian.common.flags` 下的 `flag.<name>`，返回 `pending-publish`，没有发布 API。发布由独立 Portal 操作员人工完成（S1-DEC-031）。

当前源码实现与真实服务验收不等于已发布包源。保留 `x-skeleton`，正式发行与 `qian add` 安装门通过后再解除；默认组合包不自动加入此管理写端。现有 `qian add config` 的只读配置组件不因此获得写入权限。

宿主装配、严格规则、权限与持久回执语义见 [Provider 主文档](../../packages/Qian.Config.Apollo/README.md#特性开关与草稿管理)。
