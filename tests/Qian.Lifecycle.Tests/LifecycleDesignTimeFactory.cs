using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;

namespace Qian.Lifecycle.Tests;

// dotnet ef 的设计时入口：经 AddQianData 的同一装配取上下文（命名约定、租户过滤、Qian 迁移生成器），迁移模型与运行时一致。不连接数据库：
//   dotnet ef migrations has-pending-model-changes --project packages/Qian.Lifecycle --startup-project tests/Qian.Lifecycle.Tests --context LifecycleDbContext
internal sealed class LifecycleDesignTimeFactory : IDesignTimeDbContextFactory<LifecycleDbContext>
{
    public LifecycleDbContext CreateDbContext(string[] args)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        services.AddKunEventBus();
        services.AddQianData<LifecycleDbContext>((_, options) => options.UseNpgsql("Host=localhost;Database=design;Username=design").UseKunSchema(LifecycleDbContext.Schema));
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<LifecycleDbContext>();
    }
}
