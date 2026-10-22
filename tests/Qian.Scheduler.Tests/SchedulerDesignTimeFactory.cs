using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;

namespace Qian.Scheduler.Tests;

internal sealed class SchedulerDesignTimeFactory : IDesignTimeDbContextFactory<SchedulerDbContext>
{
    public SchedulerDbContext CreateDbContext(string[] args)
    {
        var services = new ServiceCollection(); services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext()); services.AddKunEventBus();
        services.AddQianData<SchedulerDbContext>((_, options) => options.UseNpgsql("Host=localhost;Database=design;Username=design").UseKunSchema(SchedulerDbContext.Schema));
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<SchedulerDbContext>();
    }
}
