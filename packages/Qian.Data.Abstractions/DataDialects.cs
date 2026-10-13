using Qian.Config;

namespace Qian.Data;

/// <summary>方言 SPI 的稳定标识与注册表名。</summary>
public static class DataDialectContract
{
    /// <summary>扩展点 ID（plugin-manifest 的 S1 扩展点之一）。</summary>
    public const string ExtensionPoint = "qian.data.dialect/v1";
    /// <summary>Data seam 的方言注册表子服务；具名注册写作 <c>Data.Dialects:&lt;name&gt;</c>（R38）。</summary>
    public const string Registry = "Data.Dialects";
}

/// <summary>连通测试的目标；不含凭据。</summary>
public sealed record DataConnectionTarget(string Host, int Port, string Database, string UserName, string? Schema = null, string? Encoding = null);

/// <summary>一次成功探测得到的服务端信息。</summary>
public sealed record DataConnectionProbe(string ServerVersion);

/// <summary>方言 SPI（qian.data.dialect/v1）：方言组件提供描述数据（驱动、连接串模板、类型映射）与连通探测。</summary>
public interface IDataDialect
{
    /// <summary>方言描述数据；代码即注册名。</summary>
    DbDialect Descriptor { get; }

    /// <summary>
    /// 用驱动的连接串构建器（不拼接字符串）打开一次连接并执行只读探测，超过 <paramref name="timeout"/> 视为不可达。
    /// 网络不可达或超时抛 DATA_CONNECTION_UNREACHABLE，服务端拒绝（认证、库、schema、权限、参数）抛 DATA_CONNECTION_REJECTED；
    /// 错误不得携带口令或驱动原文。
    /// </summary>
    ValueTask<DataConnectionProbe> ProbeAsync(DataConnectionTarget target, SecretValue password, TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>Data.Dialects 注册表（R38 注册表 seam）：按名称独占注册，返回的句柄释放即撤销。</summary>
public interface IDataDialectRegistry
{
    /// <summary>注册方言；名称须与描述数据的代码一致，同名第二次注册失败，描述数据不合法时拒绝。</summary>
    IDisposable Register(string name, IDataDialect dialect);
    /// <summary>按名称查找已注册的方言。</summary>
    IDataDialect? Find(string name);
    /// <summary>已注册方言的描述数据，按代码排序。</summary>
    IReadOnlyList<DbDialect> List();
}
