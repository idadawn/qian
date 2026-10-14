using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qian.Capabilities.AspNetCore;

// 目录信封的线上形状（contracts/v0/capability-catalog.schema.json）：camelCase 成员，与宿主 JSON 选项无关。
// 保留元数据生成：ASP.NET Core 以异步流写出响应，不能只靠快速路径序列化。
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(CapabilityCatalog))]
internal sealed partial class CapabilityHttpJson : JsonSerializerContext;
