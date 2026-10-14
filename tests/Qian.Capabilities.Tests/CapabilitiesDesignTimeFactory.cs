using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;

namespace Qian.Capabilities.Tests;

// dotnet ef 的设计时入口：经 AddQianData 的同一装配取上下文（命名约定、租户过滤、Qian 迁移生成器），迁移模型与运行时一致。
// 不连接数据库；连接串只用于选定 Npgsql 提供程序。用法见 packages/Qian.Data/README.md「迁移与 RLS」：
//   dotnet ef migrations has-pending-model-changes --project packages/Qian.Capabilities --startup-project tests/Qian.Capabilities.Tests --context CapabilitiesDbContext
internal sealed class CapabilitiesDesignTimeFactory : IDesignTimeDbContextFactory<CapabilitiesDbContext>
{
    public CapabilitiesDbContext CreateDbContext(string[] args)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        services.AddKunEventBus();
        services.AddQianData<CapabilitiesDbContext>((_, options) => options.UseNpgsql("Host=localhost;Database=design;Username=design").UseKunSchema(CapabilitiesDbContext.Schema));
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<CapabilitiesDbContext>();
    }
}
