using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;

namespace Qian.Identity.Server.Tests;

// 与运行时相同的数据安全装配；只生成模型，不连接数据库。
internal sealed class IdentityDesignTimeFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var services = new ServiceCollection(); services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        services.AddKunEventBus();
        services.AddQianData<IdentityDbContext>((_, options) => options.UseNpgsql("Host=localhost;Database=design;Username=design").UseKunSchema(IdentityDbContext.Schema));
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IdentityDbContext>();
    }
}
