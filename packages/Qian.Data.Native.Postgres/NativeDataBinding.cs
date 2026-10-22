using Microsoft.Extensions.DependencyInjection;

namespace Qian.Data.Native.Postgres;

/// <summary>宿主批准的连接用途，不能由请求或插件 JSON 改变。</summary>
public enum NativeDataAccess
{
    /// <summary>必须具有有效租户上下文。</summary>
    Tenant,
    /// <summary>专用平台登录，只允许有效 global 调用方且没有租户。</summary>
    Platform
}

/// <summary>固定迁移契约：精确的 pg_get_expr 输出必须来自受审查的迁移真相源，不能从当前数据库学习后回填。</summary>
public sealed record NativeDataTable(string Schema, string Name, string GuardPolicy, string ExpectedUsing, string ExpectedCheck);

/// <summary>受信任宿主的具名连接绑定。凭据工厂仅在组件挂载时读取，密钥轮换后重挂载；对象打印不暴露工厂或凭据。</summary>
public sealed record NativeDataBinding(string Name, string Role, NativeDataAccess Access,
    Func<CancellationToken, ValueTask<string>> ConnectionString, IReadOnlyList<NativeDataTable> Tables)
{
    /// <inheritdoc />
    public override string ToString() => "NativeDataBinding(redacted)";
}

/// <summary>只登记宿主信任的绑定；组件挂载时才校验数据库并提供 NativeData。</summary>
public static class NativeDataHosting
{
    /// <summary>登记一个绑定；重复名字、缺表或非保护表达式拒绝，角色与数据库的真实安全属性在挂载时验证。</summary>
    public static IServiceCollection AddQianNativeData(this IServiceCollection services, NativeDataBinding binding)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(binding);
        if (!Name(binding.Name) || !Name(binding.Role) || binding.ConnectionString is null || !Enum.IsDefined(binding.Access) ||
            binding.Tables is not { Count: > 0 } || binding.Tables.Any(table => table is null || !Name(table.Schema) || !Name(table.Name) || !Name(table.GuardPolicy) ||
                !Protection(table.ExpectedUsing) || !Protection(table.ExpectedCheck)) ||
            binding.Tables.Select(table => (table.Schema, table.Name)).Distinct().Count() != binding.Tables.Count ||
            services.Any(item => item.ServiceType == typeof(NativeDataBinding) && item.ImplementationInstance is NativeDataBinding other && other.Name == binding.Name))
            throw NativeErrors.Invalid();
        services.AddSingleton(binding with { Tables = Array.AsReadOnly(binding.Tables.ToArray()) });
        return services;
    }
    internal static bool Name(string? value) => value is { Length: > 0 and <= 63 } && value[0] is >= 'a' and <= 'z' &&
        value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-');
    // 这只是声明防误用检查；数据库必须与受审查的完整表达式逐字一致，绝不通过模糊匹配判断实际策略安全。
    private static bool Protection(string? value) => value is { Length: > 0 } && value.Contains("tenant_id", StringComparison.Ordinal) &&
        value.Contains("app.current_tenant_id", StringComparison.Ordinal) && !value.Contains(';', StringComparison.Ordinal);
}
