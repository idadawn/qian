using System.Text.Json;
using Qian.Runtime;

namespace Qian.Registry;

/// <summary>租户所属的一条有效数据库 patch；Patch 使用原有 patch/v2 格式，修订不混入该格式。</summary>
public sealed record ScopePatchSnapshot(string TenantId, ScopeKey Scope, string EntryId, JsonElement Patch,
    int Revision, string UpdatedBy, DateTimeOffset UpdatedAt)
{
    /// <summary>默认日志字符串不展开配置或 note。</summary>
    public override string ToString() => $"ScopePatchSnapshot {{ Scope = {Scope.Kind}:{Scope.Id}, EntryId = {EntryId}, Revision = {Revision} }}";
}
