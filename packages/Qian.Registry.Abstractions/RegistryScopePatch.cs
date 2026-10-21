using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qian.Registry;

/// <summary>条件写入请求；Patch 为 patch/v2 单条目，不能携带租户字段；Revision 为当前修订，创建为 0。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegistryScopePatchRequest([property: JsonRequired, Range(0, int.MaxValue)] int Revision, [property: JsonRequired] JsonElement Patch)
{
    /// <summary>默认日志不展开配置与 note。</summary>
    public override string ToString() => $"RegistryScopePatchRequest {{ Revision = {Revision} }}";
}

/// <summary>管理面写入结论；State 为 Ready、Unmounted 或 Failed，失败时 ReasonCode 指向装配诊断。</summary>
public sealed record RegistryScopePatchResult(string Scope, string EntryId, int Revision, string State, string? ReasonCode);
