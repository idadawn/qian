using System.Reflection;
using System.Text.Json;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Qian.Plugin.Sdk;
using Qian.Runtime;

namespace Qian.Data.Tests;

// S1-RT-014 验收 1（按 S1-DEC-018 回验 DATA-001）：仓库里 qian.data 唯一清单的代码字段由源生成器派生；宿主按清单的 runtime.entry
// 装载真实 Provider（不直接 new 具体类型），挂载后的数据会话默认带 AddQianData 的安全装配，组件自身不需要再 opt-in。
public sealed partial class DataSessionTests
{
    [Fact]
    public async Task 按仓库清单的代码派生入口装载真实qian_data_清单字段与生成元数据一致_数据会话默认带租户填充过滤与RLS_骨架标记保留()
    {
        var ct = TestContext.Current.CancellationToken;
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins/qian.data/.qian-plugin/plugin.json")));
        var root = manifest.RootElement;
        var factory = typeof(DataPlugin).Assembly.GetCustomAttribute<QianCodeMetadataAttribute>()!.FactoryType;
        var code = ((IComponentCodeMetadataFactory)Activator.CreateInstance(factory)!).Create();
        var entry = root.GetProperty("runtime").GetProperty("entry").GetString()!;
        Assert.Equal(("qian.data", "qian.data"), (code.ComponentId, root.GetProperty("id").GetString()));
        Assert.Equal((code.RuntimeEntry, "module"), (entry, root.GetProperty("runtime").GetProperty("form").GetString()));
        Assert.Equal(code.Inject, root.GetProperty("inject").EnumerateArray().Select(item => item.GetString()!));
        Assert.Equal(code.Provides, root.GetProperty("provides").EnumerateArray().Select(item => item.GetString()!));
        var generated = root.GetProperty("x-qian-generated");
        Assert.True(generated.GetProperty("$generated").GetBoolean());
        Assert.Equal(["/inject", "/provides", "/runtime/form", "/runtime/entry"], generated.GetProperty("fields").EnumerateArray().Select(item => item.GetString()!));
        // S1-DEC-018：真实 qian add data 安装（S1-RT-011）通过前保留骨架标记。
        Assert.True(root.GetProperty("x-skeleton").GetBoolean());

        var type = Type.GetType(entry, throwOnError: true)!;
        Assert.Same(typeof(DataPlugin), type);
        var tenant = new AsyncLocalTenantContext();
        using var host = BuildHost(tenant);
        await host.StartAsync(ct);
        await PrepareAsync(host);
        await using var runtime = new PluginLoader(host.Services);
        await runtime.MountAsync([new PluginDefinition(
            new PluginEntry("data", code.ComponentId, root.GetProperty("version").GetString()!, ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })),
            (IQianPlugin)Activator.CreateInstance(type)!, code.Provides.Select(name => new ServiceKey(name)).ToArray())], ct);
        Assert.Equal(PluginState.Ready, runtime.GetState("data", ScopeKey.Global));
        var data = runtime.GetContext("data", ScopeKey.Global).Data();

        var id = Guid.NewGuid(); string table;
        using (tenant.BeginScope("a"))
        await using (var session = await data.OpenAsync<CustomerContext>(ct))
        {
            var customer = new Customer(id, "manifest");
            session.Context.Add(customer);
            await session.SaveChangesAsync(ct);
            Assert.Equal("a", customer.TenantId);
            Assert.Equal("a", await session.Context.Database.SqlQueryRaw<string>("SELECT current_setting('app.current_tenant_id', true) AS \"Value\"").SingleAsync(ct));
            var mapping = session.Context.Model.FindEntityType(typeof(Customer))!;
            table = mapping.GetSchema() + "." + mapping.GetTableName();
        }
        using (tenant.BeginScope("b"))
        await using (var other = await data.OpenAsync<CustomerContext>(ct))
            Assert.Empty(await other.Context.Set<Customer>().Where(customer => customer.Id == id).ToListAsync(ct));
        // 绕过 EF、以受限应用角色（NOSUPERUSER / NOBYPASSRLS）直连：只有会话租户为 a 时看得到这一行。
        Assert.Equal((0L, 1L), (await RestrictedCount(table, id, "b", ct), await RestrictedCount(table, id, "a", ct)));
    }

    private async Task<long> RestrictedCount(string table, Guid id, string tenant, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(postgres.AppConnectionString);
        await connection.OpenAsync(ct);
        await using (var set = new NpgsqlCommand("SELECT set_config('app.current_tenant_id', @tenant, false)", connection))
        {
            set.Parameters.AddWithValue("tenant", tenant);
            await set.ExecuteScalarAsync(ct);
        }
        await using var count = new NpgsqlCommand($"SELECT count(*) FROM {table} WHERE id = @id", connection);
        count.Parameters.AddWithValue("id", id);
        return (long)(await count.ExecuteScalarAsync(ct))!;
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Qian.slnx"))) return directory.FullName;
        throw new InvalidOperationException("找不到仓库根目录。");
    }
}
