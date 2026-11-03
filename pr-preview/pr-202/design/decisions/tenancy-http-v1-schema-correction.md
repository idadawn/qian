> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# 租户 HTTP v1 校验声明的首次发布基线纠偏

> 本页由构建脚本镜像自仓库 [`docs/decisions/tenancy-http-v1-schema-correction.md`](https://github.com/idadawn/qian/blob/main/docs/decisions/tenancy-http-v1-schema-correction.md)，请修改源文件，不要改本页。

状态：产品负责人于 2026-10-17 附五项条件确认；S1-DEC-025 记录确认与证据，实施统一归 S1-TEN-011（承接 TEN-008 收口线），S1-TEN-005 消费最终契约。R110 所称兼容补齐不能覆盖工具已报告的 breaking。

## 逐条旧行为证据

冻结修正前服务端提交 `159bedf99a88c10d70dbc3b61f414712a7961eaa`，对应旧快照 SHA-256 `b40206dac7b1bd0b3606bfed66aa1acb639ec4003a76d35339f2689c3885db7d`。CreateAsync 在旧文件 L35 调用 ValidateCode/ValidateName，UpdateAsync 在 L53 调用 ValidateName；本次保留所有拒绝判定，只为既有 INPUT_INVALID 补字段码和 pointer。

历史用例已覆盖租户号三类拒绝；历史名称负例覆盖不足，因此先提交补证测试 `024323a0f61fd2976db429011c3f96f1904613d0`，在未修改服务端实现的基础上运行。原有租户号 1 项和名称 3 个参数场景共 4/4 通过、0 跳过，再应用纠偏代码。名称用例分别调用原 CreateAsync 与 UpdateAsync，校验拒绝后行数、值及 revision 不变。不能把这批新补测试描述为历史版本原来就有，也不以纠偏后实现的结果冒充旧行为。

| # | oasdiff 规则 ID                     | 操作 / 字段 / 差异                                                     | 修正前代码                                                                                                                                             | 修正前测试与拒绝证据                                                                                                                                                                                                |
| - | --------------------------------- | ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1 | `request-property-max-length-set` | POST /api/tenancy/tenants / code；未声明 → maxLength 64              | [code 校验 L68](https://github.com/idadawn/qian/blob/159bedf99a88c10d70dbc3b61f414712a7961eaa/packages/Qian.Tenancy/PostgresTenantDirectory.cs#L68) | [原有非法租户号用例 L73–78](https://github.com/idadawn/qian/blob/159bedf99a88c10d70dbc3b61f414712a7961eaa/tests/Qian.Tenancy.Tests/TenantDirectoryTests.cs#L73-L78)：65 个 a，INPUT_INVALID 且无入库                      |
| 2 | `request-property-max-length-set` | POST /api/tenancy/tenants / name；未声明 → maxLength 200             | [name 校验 L70](https://github.com/idadawn/qian/blob/159bedf99a88c10d70dbc3b61f414712a7961eaa/packages/Qian.Tenancy/PostgresTenantDirectory.cs#L70) | [旧实现补证 L20–36](https://github.com/idadawn/qian/blob/024323a0f61fd2976db429011c3f96f1904613d0/tests/Qian.Tenancy.Tests/TenantValidationBaselineTests.cs#L20-L36)：201 字符、101 个补充字符，创建/编辑 INPUT_INVALID，主档不变 |
| 3 | `request-property-min-length-set` | POST /api/tenancy/tenants / code；未声明 → minLength 1               | [code 校验 L68](https://github.com/idadawn/qian/blob/159bedf99a88c10d70dbc3b61f414712a7961eaa/packages/Qian.Tenancy/PostgresTenantDirectory.cs#L68) | [原有非法租户号用例 L73–78](https://github.com/idadawn/qian/blob/159bedf99a88c10d70dbc3b61f414712a7961eaa/tests/Qian.Tenancy.Tests/TenantDirectoryTests.cs#L73-L78)：空串，INPUT_INVALID 且无入库                          |
| 4 | `request-property-min-length-set` | POST /api/tenancy/tenants / name；未声明 → minLength 1               | [name 校验 L70](https://github.com/idadawn/qian/blob/159bedf99a88c10d70dbc3b61f414712a7961eaa/packages/Qian.Tenancy/PostgresTenantDirectory.cs#L70) | [旧实现补证 L20–36](https://github.com/idadawn/qian/blob/024323a0f61fd2976db429011c3f96f1904613d0/tests/Qian.Tenancy.Tests/TenantValidationBaselineTests.cs#L20-L36)：空串，创建/编辑 INPUT_INVALID，主档不变               |
| 5 | `request-property-pattern-added`  | POST /api/tenancy/tenants / name；未声明 → pattern（完整值见原始 JSON）      | [name 校验 L70](https://github.com/idadawn/qian/blob/159bedf99a88c10d70dbc3b61f414712a7961eaa/packages/Qian.Tenancy/PostgresTenantDirectory.cs#L70) | [旧实现补证 L20–36](https://github.com/idadawn/qian/blob/024323a0f61fd2976db429011c3f96f1904613d0/tests/Qian.Tenancy.Tests/TenantValidationBaselineTests.cs#L20-L36)：空白与控制字符，创建/编辑 INPUT_INVALID，主档不变          |
| 6 | `request-property-pattern-added`  | POST /api/tenancy/tenants / code；未声明 → pattern（完整值见原始 JSON）      | [code 校验 L68](https://github.com/idadawn/qian/blob/159bedf99a88c10d70dbc3b61f414712a7961eaa/packages/Qian.Tenancy/PostgresTenantDirectory.cs#L68) | [原有非法租户号用例 L73–78](https://github.com/idadawn/qian/blob/159bedf99a88c10d70dbc3b61f414712a7961eaa/tests/Qian.Tenancy.Tests/TenantDirectoryTests.cs#L73-L78)：大写、连字符、中文、空格，INPUT_INVALID 且无入库                |
| 7 | `request-property-max-length-set` | PUT /api/tenancy/tenants/\{id} / name；未声明 → maxLength 200        | [name 校验 L70](https://github.com/idadawn/qian/blob/159bedf99a88c10d70dbc3b61f414712a7961eaa/packages/Qian.Tenancy/PostgresTenantDirectory.cs#L70) | [旧实现补证 L20–36](https://github.com/idadawn/qian/blob/024323a0f61fd2976db429011c3f96f1904613d0/tests/Qian.Tenancy.Tests/TenantValidationBaselineTests.cs#L20-L36)：201 字符、101 个补充字符，创建/编辑 INPUT_INVALID，主档不变 |
| 8 | `request-property-min-length-set` | PUT /api/tenancy/tenants/\{id} / name；未声明 → minLength 1          | [name 校验 L70](https://github.com/idadawn/qian/blob/159bedf99a88c10d70dbc3b61f414712a7961eaa/packages/Qian.Tenancy/PostgresTenantDirectory.cs#L70) | [旧实现补证 L20–36](https://github.com/idadawn/qian/blob/024323a0f61fd2976db429011c3f96f1904613d0/tests/Qian.Tenancy.Tests/TenantValidationBaselineTests.cs#L20-L36)：空串，创建/编辑 INPUT_INVALID，主档不变               |
| 9 | `request-property-pattern-added`  | PUT /api/tenancy/tenants/\{id} / name；未声明 → pattern（完整值见原始 JSON） | [name 校验 L70](https://github.com/idadawn/qian/blob/159bedf99a88c10d70dbc3b61f414712a7961eaa/packages/Qian.Tenancy/PostgresTenantDirectory.cs#L70) | [旧实现补证 L20–36](https://github.com/idadawn/qian/blob/024323a0f61fd2976db429011c3f96f1904613d0/tests/Qian.Tenancy.Tests/TenantValidationBaselineTests.cs#L20-L36)：空白与控制字符，创建/编辑 INPUT_INVALID，主档不变          |

三种规则 ID 各出现三次，共九项 error（level=3）；每项原始 operationId、fingerprint 和约束原文保留在 [oasdiff.json](https://github.com/idadawn/qian/blob/main/docs/decisions/evidence/S1-DEC-025/oasdiff.json)。代码/名称的接受集合没有扩大或缩小；若复验出现任何实际行为变化，移出本纠偏并走 v2，不以本确认豁免。名称的历史长度单位为 UTF-16，标准 maxLength=200 是 Unicode 字符数上界；补充字符超过服务端单元数仍按原校验拒绝，本次未改计长语义。名称 pattern 显式列出原 IsWhiteSpace 集合，避免 JavaScript 与 .NET 的 `\S` 差异；末尾排除剩余字符，避免 `$` 接受结尾换行。

## 快照与差异留痕

- [修正前快照](https://github.com/idadawn/qian/blob/main/docs/decisions/evidence/S1-DEC-025/before.openapi.json)、[统一修正后快照](https://github.com/idadawn/qian/blob/main/docs/decisions/evidence/S1-DEC-025/after.openapi.json)、[逐行差异](https://github.com/idadawn/qian/blob/main/docs/decisions/evidence/S1-DEC-025/snapshot.diff)、[摘要与观测记录](https://github.com/idadawn/qian/blob/main/docs/decisions/evidence/S1-DEC-025/manifest.json)。证据副本只供复核，不作为运行时第二基线。
- 修正后 SHA-256：`d8132eac845614e901d0d47443cc7eade855f5527f93038892becef1c9cbb528`。
- 除九项既有校验声明，统一差异还包括 R110 的可选筛选、稳定分页总数、状态机响应 Schema，以及 TEN-011 的输入/输出 Schema 分离、去除 CLR int 上限噪音。它们没有产生额外 breaking；新接口行为分别按 TEN-011 的真实宿主用例验收，不能混称为旧行为。
- 复现：`oasdiff breaking before.openapi.json after.openapi.json --format json`。旧到新仍报告这九项，不声称历史静态比较通过。工具版本锁定 1.33.0；通用准入仍拒绝破坏性候选，真实测试保留删除 PUT、修改响应类型两个负例。

## 五项确认条件与执行边界

1. 逐条证据以上表为准；实际行为变化必须拆分走版本。
2. 原始规则 ID、前后快照及完整差异随 S1-DEC-025 留存，TEN-011 和决策 PR 描述列出九项。不得用摘要替换原始报告。
3. 不增加 ignore、不改 fail-on/severity、不放宽准入探针或测试。修正后的 v1 是尚未发布的首个基线，成立依据是未进入平台契约仓库；不是给已有基线绕过兼容检查。
4. 只此一次。TEN-008 只补排除验收；TEN-011 将生成器修复、既有校验声明与 R110 后端补齐一次写入正式 v1 快照；TEN-005 不再修改该基线。此后 v1 任何收窄必须升级 v2，并提供 R33 的别名、Deprecation 与 `Link: <后继路径>; rel="successor-version"`。契约发布前必须带齐服务端校验约束，并有真实端点/快照一致性与边界拒绝测试，这是 TEN-008/TEN-011 契约收口的必过项。
5. 时间窗止于任何 qian.tenancy 制品进入平台契约仓库之前。RT-013 的 publish 或 DEP-005 发布链一旦跑过，或发现任何已登记基线，本次资格立即失效，收窄只能走 v2。合入前再次核对，不能只凭任务 todo 判断。

## 发布窗口观测

2026-10-16 17:18–17:19 UTC，只读检查 qd 集群：qian-prod 和 qian-staging 仅有 PostgreSQL/Redis 基础设施，qian 数据库 `pg_tables` 除系统 schema 外均为空；没有平台契约表、qian-platform 工作负载或对应 Argo Application。仅有 Qian 文档站、看板和两个 baseline Application。CLI 当前无 publish 动词实现，RT-013/DEP-005 均为 todo，仓库工作流列表没有该发布链。

存在 [2026-10-14 的 NuGet 包族发布](https://github.com/idadawn/qian/actions/runs/38646939291)，因此不能声称所有 Qian 包从未发布。该流程推送 NuGet 包并做干净消费者还原，不登记平台契约、不执行 RT-013 publish 或 DEP-005 发布链；本次依据的是上面的平台契约仓库与执行链检查。GitHub Releases 为空也只作辅助证据，不单独据此推断未发布。

TEN-011 最终合入前须重查两个业务库、平台工作负载、RT-013/DEP-005 与发布流水线；任一观测未知或出现已发布记录就不得按纠偏覆盖 v1。后续发布任务必须先核对本次批准、完整快照、兼容准入与真实校验测试，不得先发布再补约束。
