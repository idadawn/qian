using System.Text.Json;
using System.Text.Json.Serialization;
using Qian.Runtime;

namespace Qian.Tenancy;

/// <summary>租户设置与配额的服务键。</summary>
public static class TenantSettings
{
    /// <summary>当前租户的设置读模型。</summary>
    public static readonly ServiceKey Key = new("Tenancy.Settings");
    /// <summary>平台运营设置端口。</summary>
    public static readonly ServiceKey OperationsKey = new("Tenancy.SettingsOperations");
    /// <summary>当前租户的配额判定端口。</summary>
    public static readonly ServiceKey QuotaKey = new("Tenancy.Quota");
    /// <summary>Identity 的可选真实人数统计端口。</summary>
    public static readonly ServiceKey UserCounterKey = new("Tenancy.UserCounter");
}
/// <summary>只读取当前可信租户的运行时合成配置；不保存第二份配置表。</summary>
public interface ITenantSettings
{
    /// <summary>按点或冒号分隔的配置路径读取；缺失时回退平台配置源，仍缺失返回 default。</summary>
#pragma warning disable CA1716 // R97 固定公共契约名称 ITenantSettings.Get<T>。
    ValueTask<T?> Get<T>(string pluginId, string key, CancellationToken cancellationToken = default);
#pragma warning restore CA1716
}
/// <summary>平台超管已授权后的运营门面。</summary>
public interface ITenantSettingsOperations
{
    /// <summary>目标租户来自运营资源路径。</summary>
    ValueTask<TenantSettingsDocument> ReadAsync(Guid id, string pluginId, CancellationToken cancellationToken = default);
    /// <summary>配置整体替换，经 Registry.MountAsync 的原校验器与修订并发写入。</summary>
    ValueTask<TenantSettingsDocument> WriteAsync(Guid id, string pluginId, UpdateTenantSettings request, string actor, CancellationToken cancellationToken = default);
    /// <summary>读取限额与实际计数端口结果，端口缺席时 UsersUsed=null、UsageAvailable=false。</summary>
    ValueTask<TenantQuotaView> QuotaAsync(Guid id, CancellationToken cancellationToken = default);
}
/// <summary>完整设置与该 scope patch 的并发版本。</summary>
public sealed record TenantSettingsDocument(string PluginId, string EntryId, int Revision, JsonElement Config)
{
    /// <inheritdoc />
    public override string ToString() => $"TenantSettingsDocument {{ PluginId = {PluginId}, EntryId = {EntryId}, Revision = {Revision} }}";
}
/// <summary>设置输入不允许目标租户字段。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateTenantSettings(JsonElement Config, int ExpectedRevision)
{
    /// <inheritdoc />
    public override string ToString() => "UpdateTenantSettings(redacted)";
}
/// <summary>主档限额，null 表示没有该项上限。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TenantQuotas(int? Users = null);
/// <summary>用量缺席不能报告为零。</summary>
public sealed record TenantQuotaView(TenantQuotas Limits, int? UsersUsed, bool UsageAvailable);
/// <summary>服务端已验证的成员类别，不接受 HTTP 请求自行声称是平台超管。</summary>
public enum TenantMemberKind
{
    /// <summary>普通租户成员，计入。</summary>
    Member,
    /// <summary>租户管理员，计入。</summary>
    Administrator,
    /// <summary>平台超管，不是租户成员，不计入。</summary>
    PlatformSuperadmin
}
/// <summary>Identity 提供的真实租户成员统计，须排除平台超管并包含租户管理员。</summary>
public interface ITenantUserCounter
{
    /// <summary>只统计当前租户；返回值不可由客户端提交。</summary>
    ValueTask<int> CountUsersAsync(CancellationToken cancellationToken = default);
}
/// <summary>成员创建前的配额判定；计数、锁和实际创建由 Identity 同一事务承担。</summary>
public interface ITenantQuota
{
    /// <summary>currentUsers 与 kind 必须来自服务端身份目录；超限抛 CARDINALITY_USER_LIMIT。</summary>
    ValueTask EnsureCanAddUserAsync(int currentUsers, TenantMemberKind kind, CancellationToken cancellationToken = default);
}
