using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Qian.Runtime;

namespace Qian.Tenancy;

/// <summary>租户目录 seam；仅提供内部服务端投影，不发布匿名 HTTP 租户列表。</summary>
public static class TenancyDefinition
{
    /// <summary>目录服务键。</summary>
    public static readonly ServiceKey Key = new("Tenancy");
    /// <summary>当前 scope 可见的目录。</summary>
    public static ITenantDirectory Tenancy(this IPluginContext context) => context.Inject<ITenantDirectory>(Key);
}

/// <summary>身份及开户组件消费的主档目录；不包含管理员、联系方式、数据库凭据。</summary>
public interface ITenantDirectory
{
    /// <summary>按不可变的小写字母数字租户号读取；未知返回 null。</summary>
    ValueTask<TenantDescriptor?> GetAsync(string code, CancellationToken cancellationToken = default);
    /// <summary>按已验证主体的租户 ID 读取主档；此内部服务端口不构成匿名 HTTP 查询。</summary>
    ValueTask<TenantDescriptor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>租户主档的非敏感投影。</summary>
public sealed record TenantDescriptor(Guid Id, string Code, string Name, TenantIsolation Isolation, TenantStatus Status)
{
    /// <summary>当前主档的乐观并发版本；编辑请求须回传。</summary>
    public uint Revision { get; init; }
}

/// <summary>创建时决定的隔离策略；本期在线实现只接受 row。</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TenantIsolation>))]
public enum TenantIsolation
{
    /// <summary>共享库、行级过滤与 RLS。</summary>
    [JsonStringEnumMemberName("row")] Row,
    /// <summary>分 schema，尚未开放创建。</summary>
    [JsonStringEnumMemberName("schema")] Schema,
    /// <summary>独立库，S3 开放。</summary>
    [JsonStringEnumMemberName("database")] Database
}

/// <summary>生命周期状态；本任务仅创建 pending，迁移由租户生命周期实现。</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TenantStatus>))]
public enum TenantStatus
{
    /// <summary>待激活。</summary>
    [JsonStringEnumMemberName("pending")] Pending,
    /// <summary>试用。</summary>
    [JsonStringEnumMemberName("trial")] Trial,
    /// <summary>正常。</summary>
    [JsonStringEnumMemberName("active")] Active,
    /// <summary>宽限。</summary>
    [JsonStringEnumMemberName("grace")] Grace,
    /// <summary>冻结。</summary>
    [JsonStringEnumMemberName("suspended")] Suspended
}

/// <summary>新建租户请求；目标租户 ID 由服务端生成，不能经请求输入。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateTenant(
    [property: MinLength(1), MaxLength(64), RegularExpression(@"^[a-z0-9]+$(?![\s\S])")] string Code,
    [property: MinLength(1), MaxLength(200), RegularExpression(@"^(?=[\s\S]*[^\u0009-\u000D\u0020\u0085\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000])[^\u0000-\u001F\u007F-\u009F]*$(?![\s\S])")] string Name,
    [property: AllowedValues(TenantIsolation.Row)] TenantIsolation Isolation = TenantIsolation.Row)
{
    /// <summary>运营开户的管理员指针；HTTP 新建必填，不代表 Identity 已创建 Membership。</summary>
    [JsonRequired, DisallowNull]
    public TenantAdministrator? Administrator { get; init; }
    /// <summary>初始限额；空表示无限额。</summary>
    public TenantQuotas? Quotas { get; init; }
}

/// <summary>编辑主档；租户号和隔离策略不在可编辑字段中。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateTenant(
    [property: MinLength(1), MaxLength(200), RegularExpression(@"^(?=[\s\S]*[^\u0009-\u000D\u0020\u0085\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000])[^\u0000-\u001F\u007F-\u009F]*$(?![\s\S])")] string Name,
    uint ExpectedRevision)
{
    /// <summary>非空时替换限额；省略保持原值。</summary>
    public TenantQuotas? Quotas { get; init; }
}

/// <summary>事件/作业 JSON 与 gRPC 文本元数据共用的租户传播端口；不替代传输层认证。</summary>
public interface ITenantContextPropagator
{
    /// <summary>从当前可信上下文写入 tenant_id；已有不同值、重复字段或无租户时拒绝。</summary>
    JsonElement Write(JsonElement carrier);
    /// <summary>从当前可信上下文生成只包含 tenant_id 的传输头。</summary>
    IReadOnlyDictionary<string, string> WriteHeaders();
    /// <summary>消费已认证来源的 JSON；authorizedTenantId 必须来自令牌或受保护调度记录，不能取自同一输入。</summary>
    IDisposable BeginScope(JsonElement carrier, Guid authorizedTenantId);
    /// <summary>消费已认证来源的元数据；保留重复项以便拒绝重复 tenant_id。</summary>
    IDisposable BeginScope(IEnumerable<KeyValuePair<string, string>> headers, Guid authorizedTenantId);
}
