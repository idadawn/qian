using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qian.Lifecycle.AspNetCore;

// 管理面的线上形状：camelCase 成员，与宿主 JSON 选项无关；保留元数据生成以便异步流写出。批准请求体不接受未知成员（含租户字段）。
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(LifecycleDiagnosticList))]
[JsonSerializable(typeof(PermissionGrants))]
[JsonSerializable(typeof(PermissionApprovalView))]
internal sealed partial class LifecycleHttpJson : JsonSerializerContext;
