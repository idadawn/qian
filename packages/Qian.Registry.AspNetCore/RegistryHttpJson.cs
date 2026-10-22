using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qian.Registry.AspNetCore;

// 管理面视图的线上形状：camelCase 成员，与宿主 JSON 选项无关。
// 保留元数据生成：ASP.NET Core 以异步流写出响应，不能只靠快速路径序列化。
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(RegistryEntryList))]
[JsonSerializable(typeof(RegistryEntryView))]
[JsonSerializable(typeof(RegistryArtifactPermissions))]
[JsonSerializable(typeof(RegistryScopePatchRequest))]
[JsonSerializable(typeof(RegistryScopePatchResult))]
internal sealed partial class RegistryHttpJson : JsonSerializerContext;
