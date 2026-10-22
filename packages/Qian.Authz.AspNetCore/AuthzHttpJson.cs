using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qian.Authz.AspNetCore;

// me/permissions 的线上形状：camelCase 成员，与宿主 JSON 选项无关；风险按 Definition 的枚举名写出（low / medium / high / critical）。
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(AuthzPermissionList))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class AuthzHttpJson : JsonSerializerContext;
