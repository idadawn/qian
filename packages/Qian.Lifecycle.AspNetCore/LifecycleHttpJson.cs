using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qian.Lifecycle.AspNetCore;

// 管理面诊断的线上形状：camelCase 成员，与宿主 JSON 选项无关；保留元数据生成以便异步流写出。
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(LifecycleDiagnosticList))]
internal sealed partial class LifecycleHttpJson : JsonSerializerContext;
