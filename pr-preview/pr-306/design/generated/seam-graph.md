> For AI agents: the complete documentation index is available at /pr-preview/pr-306/llms.txt, the full documentation bundle is available at /pr-preview/pr-306/llms-full.txt.

# seam 图

> 本页由构建脚本镜像自仓库 [`docs/generated/seam-graph.md`](https://github.com/idadawn/qian/blob/main/docs/generated/seam-graph.md)，请修改源文件，不要改本页。

由 `qian docs generate` 派生；profile：`platform`。请修改输入后重新生成。

由清单 inject/optionalInject/provides 派生；同 scope 优先，其次 global。optionalInject 的消费方标注「（可选）」并以虚线连接：Provider 缺席时仍装载，在树里时先于消费方装载。骨架的空键集合不代表未来没有依赖。

| 服务键                         | Provider 条目           | Consumer 条目                                                                                                                                 |
| --------------------------- | --------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| `Registry`                  | `global/registry`     | `global/lifecycle`、`global/capabilities`                                                                                                    |
| `Lifecycle`                 | `global/lifecycle`    |                                                                                                                                             |
| `Capabilities`              | `global/capabilities` |                                                                                                                                             |
| `ContractAdmission`         | `global/capabilities` |                                                                                                                                             |
| `Contracts`                 | `global/capabilities` |                                                                                                                                             |
| `Navigation`                | `global/capabilities` |                                                                                                                                             |
| `CommandTenancy`            | `global/tenancy`      | `global/capabilities`                                                                                                                       |
| `Tenancy`                   | `global/tenancy`      | `global/capabilities`、`global/scheduler`                                                                                                    |
| `TenancyAdministration`     | `global/tenancy`      |                                                                                                                                             |
| `TenancyLifecycle`          | `global/tenancy`      |                                                                                                                                             |
| `TenancyOperations`         | `global/tenancy`      |                                                                                                                                             |
| `TenancyProvisioning`       | `global/tenancy`      |                                                                                                                                             |
| `TenancyQuota`              | `global/tenancy`      |                                                                                                                                             |
| `TenancySettings`           | `global/tenancy`      |                                                                                                                                             |
| `TenancySettingsOperations` | `global/tenancy`      |                                                                                                                                             |
| `TenancySummary`            | `global/tenancy`      |                                                                                                                                             |
| `Identity`                  | `global/identity`     |                                                                                                                                             |
| `Authz`                     | `global/authz`        |                                                                                                                                             |
| `AuthzEnforcement`          | `global/authz`        |                                                                                                                                             |
| `AuthzPermissions`          | `global/authz`        |                                                                                                                                             |
| `AuthzResourceTypes`        | `global/authz`        |                                                                                                                                             |
| `AuthzSuperadminBypass`     | `global/authz`        | `global/lifecycle`（可选）                                                                                                                      |
| `AuthzSuperadminCheck`      | `global/authz`        | `global/capabilities`（可选）                                                                                                                   |
| `AuthzTenantRoleGrant`      | `global/authz`        |                                                                                                                                             |
| `CommandAuthorization`      | `global/authz`        | `global/capabilities`                                                                                                                       |
| `Data`                      | `global/data`         | `global/registry`、`global/lifecycle`、`global/capabilities`、`global/tenancy`、`global/authz`、`global/files`、`global/audit`、`global/scheduler` |
| `Files`                     | `global/files`        |                                                                                                                                             |
| `Files.Backends:local`      | `global/files`        |                                                                                                                                             |
| `Config`                    | `global/config`       | `global/lifecycle`                                                                                                                          |
| `Events`                    | `global/events`       |                                                                                                                                             |
| `Audit`                     | `global/audit`        | `global/capabilities`、`global/lifecycle`（可选）                                                                                                |
| `CommandAudit`              | `global/audit`        | `global/capabilities`                                                                                                                       |
| `Scheduler`                 | `global/scheduler`    |                                                                                                                                             |
| `Shell`                     | `global/shell`        |                                                                                                                                             |

```mermaid
flowchart LR
  n0["global/registry"]
  n1["global/lifecycle"]
  n2["global/capabilities"]
  n3["global/hooks"]
  n4["global/tenancy"]
  n5["global/identity"]
  n6["global/authz"]
  n7["global/data"]
  n8["global/files"]
  n9["global/config"]
  n10["global/events"]
  n11["global/audit"]
  n12["global/observability"]
  n13["global/notify"]
  n14["global/notify.inbox"]
  n15["global/notify.email"]
  n16["global/notify.sms"]
  n17["global/scheduler"]
  n18["global/realtime"]
  n19["global/entitlement"]
  n20["global/shell"]
  s0(("Registry"))
  n0 --> s0
  s1(("Lifecycle"))
  n1 --> s1
  s2(("Capabilities"))
  n2 --> s2
  s3(("ContractAdmission"))
  n2 --> s3
  s4(("Contracts"))
  n2 --> s4
  s5(("Navigation"))
  n2 --> s5
  s6(("CommandTenancy"))
  n4 --> s6
  s7(("Tenancy"))
  n4 --> s7
  s8(("TenancyAdministration"))
  n4 --> s8
  s9(("TenancyLifecycle"))
  n4 --> s9
  s10(("TenancyOperations"))
  n4 --> s10
  s11(("TenancyProvisioning"))
  n4 --> s11
  s12(("TenancyQuota"))
  n4 --> s12
  s13(("TenancySettings"))
  n4 --> s13
  s14(("TenancySettingsOperations"))
  n4 --> s14
  s15(("TenancySummary"))
  n4 --> s15
  s16(("Identity"))
  n5 --> s16
  s17(("Authz"))
  n6 --> s17
  s18(("AuthzEnforcement"))
  n6 --> s18
  s19(("AuthzPermissions"))
  n6 --> s19
  s20(("AuthzResourceTypes"))
  n6 --> s20
  s21(("AuthzSuperadminBypass"))
  n6 --> s21
  s22(("AuthzSuperadminCheck"))
  n6 --> s22
  s23(("AuthzTenantRoleGrant"))
  n6 --> s23
  s24(("CommandAuthorization"))
  n6 --> s24
  s25(("Data"))
  n7 --> s25
  s26(("Files"))
  n8 --> s26
  s27(("Files.Backends:local"))
  n8 --> s27
  s28(("Config"))
  n9 --> s28
  s29(("Events"))
  n10 --> s29
  s30(("Audit"))
  n11 --> s30
  s31(("CommandAudit"))
  n11 --> s31
  s32(("Scheduler"))
  n17 --> s32
  s33(("Shell"))
  n20 --> s33
  s25 --> n0
  s28 --> n1
  s25 --> n1
  s0 --> n1
  s30 --> n2
  s31 --> n2
  s24 --> n2
  s6 --> n2
  s25 --> n2
  s0 --> n2
  s7 --> n2
  s25 --> n4
  s25 --> n6
  s25 --> n8
  s25 --> n11
  s25 --> n17
  s7 --> n17
  s30 -.-> n1
  s21 -.-> n1
  s22 -.-> n2
```
