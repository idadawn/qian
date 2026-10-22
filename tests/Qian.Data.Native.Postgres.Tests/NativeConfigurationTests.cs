using System.Security.Cryptography;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Data.Native.Postgres.Tests;

public sealed class NativeConfigurationTests
{
    [Theory]
    [InlineData("No Reset On Close=true")]
    [InlineData("Multiplexing=true")]
    [InlineData("Options=-c statement_timeout=1000")]
    public async Task 不安全连接选项在联网前被拒绝(string option)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(ErrorCatalog.CreateDefault());
        services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var connection = "Host=127.0.0.1;Port=1;Database=unused;Username=native_user;Password=" + password + ";" + option;
        const string predicate = "tenant_id = current_setting('app.current_tenant_id')";
        services.AddQianNativeData(new("tenant", "native_user", NativeDataAccess.Tenant, _ => ValueTask.FromResult(connection),
            [new("plugin_test", "items", "guard", predicate, predicate)]));
        await using var provider = services.BuildServiceProvider(); await using var loader = new PluginLoader(provider);
        using var config = JsonDocument.Parse("""{"connections":["tenant"]}""");
        await loader.MountAsync([new(new PluginEntry("native", "qian.data.native", "1.0.0", ScopeKey.Global, 0, config.RootElement.Clone()),
            new NativeDataComponent(), [NativeDataDefinition.Key])], TestContext.Current.CancellationToken);
        Assert.Equal(PluginState.Failed, loader.GetState("native", ScopeKey.Global));
        var error = Assert.IsType<QianErrorException>(loader.GetFailure("native", ScopeKey.Global));
        Assert.Equal("NATIVE_DATA_MISCONFIGURED", error.Error.Code.Code);
        Assert.DoesNotContain(password, error.ToString(), StringComparison.Ordinal);
    }
}
