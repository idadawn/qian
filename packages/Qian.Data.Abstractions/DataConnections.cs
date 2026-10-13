using System.Text.Json.Serialization;

namespace Qian.Data;

/// <summary>新建或整体替换数据连接档案的输入。口令只以租户 secretRef 引用给出；未声明的成员（如 password）在反序列化时即被拒绝。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DataConnectionDraft(string Name, string Dialect, string Host, int Port, string Database, string UserName,
    string SecretRef, string? Schema = null, string? Encoding = null);

/// <summary>数据连接档案详情：只含凭证引用，任何情况下都不含口令。</summary>
public sealed record DataConnectionProfile(Guid Id, string Name, string Dialect, string Host, int Port, string Database,
    string UserName, string SecretRef, string? Schema, string? Encoding, DateTime CreatedAt, DateTime? UpdatedAt);

/// <summary>当前租户的连接档案列表。</summary>
public sealed record DataConnectionList(IReadOnlyList<DataConnectionProfile> Items);

/// <summary>连通测试成功的结果；失败以结构化错误返回。</summary>
public sealed record DataConnectionTestResult(string Dialect, string ServerVersion, int LatencyMs);

/// <summary>按租户隔离的数据连接档案（PRD 7.6）：凭证只存 secretRef，测试时在使用点解析，详情不回传密钥。</summary>
public interface IDataConnectionRegistry
{
    /// <summary>当前租户的全部档案，按名称排序。</summary>
    Task<DataConnectionList> ListAsync(CancellationToken cancellationToken = default);
    /// <summary>读取档案；不存在或属于其他租户时抛 DATA_CONNECTION_NOT_FOUND。</summary>
    Task<DataConnectionProfile> GetAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>新建档案；输入不合法抛 INPUT_INVALID（errors[] 指向成员），同名抛 DATA_CONNECTION_NAME_CONFLICT。</summary>
    Task<DataConnectionProfile> CreateAsync(DataConnectionDraft draft, CancellationToken cancellationToken = default);
    /// <summary>整体替换档案；校验规则同新建，并发修改抛 CONFLICT。</summary>
    Task<DataConnectionProfile> UpdateAsync(Guid id, DataConnectionDraft draft, CancellationToken cancellationToken = default);
    /// <summary>删除档案（软删）。</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>解析凭证并经方言打开一次连接；成功返回服务端版本与耗时，失败抛 DATA_CONNECTION_UNREACHABLE / DATA_CONNECTION_REJECTED 或密钥错误。</summary>
    Task<DataConnectionTestResult> TestAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>数据组件端点使用的权限点（&lt;component-id&gt;:&lt;resource&gt;:&lt;action&gt;），与 plugins/qian.data/metadata/permissions.json 一致。</summary>
public static class DataPermissions
{
    /// <summary>读取基准类型、方言与映射。</summary>
    public const string TypeRead = "qian.data:type:read";
    /// <summary>删除基准类型（系统类型始终拒绝）。</summary>
    public const string TypeDelete = "qian.data:type:delete";
    /// <summary>查看本租户的数据连接档案。</summary>
    public const string ConnectionRead = "qian.data:connection:read";
    /// <summary>新建、修改、删除本租户的数据连接档案。</summary>
    public const string ConnectionWrite = "qian.data:connection:write";
    /// <summary>对数据连接执行连通测试（会从平台出网）。</summary>
    public const string ConnectionTest = "qian.data:connection:test";
}
