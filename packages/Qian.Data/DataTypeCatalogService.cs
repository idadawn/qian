using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Qian.Runtime.Errors;

namespace Qian.Data;

/// <summary>基准类型目录与方言注册表的宿主装配。</summary>
public static class QianDataTypeExtensions
{
    /// <summary>
    /// 登记 <see cref="IDataTypeCatalog"/> 与 <see cref="IDataDialectRegistry"/>，不需要数据库。系统基准类型来自 Definition 包的种子；
    /// 方言组件以 <see cref="IDataDialect"/> 登记（如 AddQianPgDialect），注册表创建时按描述数据的代码注册。
    /// </summary>
    public static IServiceCollection AddQianDataTypes(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IDataDialectRegistry>(sp => new DataDialectRegistry(sp.GetServices<IDataDialect>()));
        services.TryAddSingleton<IDataTypeCatalog>(sp => new DataTypeCatalogService(sp.GetRequiredService<IDataDialectRegistry>()));
        services.TryAddSingleton(ErrorCatalog.CreateDefault());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, DataTypeErrorRegistration>());
        return services;
    }
}

/// <summary>Data.Dialects 注册表：按名称独占，句柄释放即撤销；注册时按系统基准类型核对描述数据。</summary>
internal sealed class DataDialectRegistry : IDataDialectRegistry
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (IDataDialect Dialect, object Owner)> _dialects = new(StringComparer.Ordinal);

    public DataDialectRegistry(IEnumerable<IDataDialect> dialects)
    {
        ArgumentNullException.ThrowIfNull(dialects);
        // 宿主经依赖注入登记的方言随注册表常驻，不需要撤销。
        foreach (var dialect in dialects) _ = Register(dialect.Descriptor?.Code ?? "", dialect);
    }

    public IDisposable Register(string name, IDataDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(dialect);
        var descriptor = dialect.Descriptor ?? throw new QianDataMisconfiguredException("方言缺少描述数据。");
        if (name != descriptor.Code) throw new QianDataMisconfiguredException("方言注册名必须与描述数据的代码一致。");
        descriptor.Validate(DataTypeContracts.SystemTypes);
        var owner = new object();
        lock (_gate)
        {
            if (!_dialects.TryAdd(name, (dialect, owner)))
                throw new QianErrorException(new QianError(PlatformErrors.RegistrationConflict, new Dictionary<string, string>
                {
                    ["key"] = DataDialectContract.Registry + ":" + name,
                    ["owner"] = _dialects[name].Dialect.Descriptor.Name
                }));
        }
        return new Lease(() =>
        {
            lock (_gate)
                if (_dialects.TryGetValue(name, out var entry) && ReferenceEquals(entry.Owner, owner)) _dialects.Remove(name);
        });
    }

    public IDataDialect? Find(string name)
    {
        lock (_gate) return name is not null && _dialects.TryGetValue(name, out var entry) ? entry.Dialect : null;
    }

    public IReadOnlyList<DbDialect> List()
    {
        lock (_gate) return Array.AsReadOnly(_dialects.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value.Dialect.Descriptor).ToArray());
    }

    private sealed class Lease(Action release) : IDisposable
    {
        private Action? _release = release;
        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}

/// <summary>基准类型目录：系统类型取种子，方言取注册表；S1 没有自定义类型，删除只会因系统类型受保护或类型不存在而失败。</summary>
internal sealed class DataTypeCatalogService(IDataDialectRegistry dialects) : IDataTypeCatalog
{
    private static readonly Dictionary<string, BaseDataType> Types =
        DataTypeContracts.SystemTypes.ToDictionary(type => type.Code, StringComparer.Ordinal);
    // 类型代码受 Schema 约束为小写字母与数字，可直接拼成 JSON 字符串。
    private static readonly JsonElement[] TypeCodes = ParseCodes();

    public DataTypeCatalog Snapshot() => new(DataTypeContracts.SystemTypes, dialects.List());

    public BaseDataType? FindType(string code) => code is not null && Types.TryGetValue(code, out var type) ? type : null;

    public DataFieldMapping MapField(string dialect, DataFieldDefinition field)
    {
        ArgumentNullException.ThrowIfNull(field);
        var descriptor = dialects.Find(dialect)?.Descriptor
            ?? throw new QianErrorException(new QianError(DataErrors.DialectNotFound, new Dictionary<string, string> { ["dialect"] = dialect ?? "" }));
        if (FindType(field.Type) is not { } type)
            throw new QianErrorException(new QianError(DataErrors.FieldDefinitionInvalid, new Dictionary<string, string> { ["type"] = field.Type ?? "" })
            {
                Errors = [new QianFieldError(PlatformErrors.ValueNotAllowed, "#/type") { Allowed = TypeCodes }]
            });
        var mapping = descriptor.Mappings.Single(candidate => candidate.Type == type.Code);
        return mapping.Map(type, field);
    }

    public ValueTask DeleteTypeAsync(string code, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var args = new Dictionary<string, string> { ["type"] = code ?? "" };
        if (FindType(code!) is { IsSystem: true }) throw new QianErrorException(new QianError(DataErrors.TypeProtected, args));
        throw new QianErrorException(new QianError(DataErrors.TypeNotFound, args));
    }

    private static JsonElement[] ParseCodes()
    {
        using var document = JsonDocument.Parse("[" + string.Join(",", DataTypeContracts.SystemTypes.Select(type => "\"" + type.Code + "\"")) + "]");
        return document.RootElement.EnumerateArray().Select(code => code.Clone()).ToArray();
    }
}

/// <summary>宿主启动时登记基准类型目录与数据连接档案的错误码，停止时撤销。</summary>
internal sealed class DataTypeErrorRegistration(ErrorCatalog catalog) : IHostedService, IDisposable
{
    private readonly List<IDisposable> _registrations = [];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_registrations.Count == 0)
            foreach (var code in DataErrors.TypesAndConnections) _registrations.Add(catalog.Register(code));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var registration in _registrations) registration.Dispose();
        _registrations.Clear();
    }
}
