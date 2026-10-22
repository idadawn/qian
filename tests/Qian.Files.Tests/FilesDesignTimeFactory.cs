using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;

namespace Qian.Files.Tests;

// dotnet ef 的设计时入口：经 AddQianData 的同一装配取上下文（命名约定、租户过滤、Qian 迁移生成器），迁移模型与运行时一致。
// 不连接数据库；连接串只用于选定 Npgsql 提供程序。用法见 packages/Qian.Data/README.md「迁移执行与 RLS 核查」：
//   dotnet ef migrations has-pending-model-changes --project packages/Qian.Files --startup-project tests/Qian.Files.Tests --context FilesDbContext
internal sealed class FilesDesignTimeFactory : IDesignTimeDbContextFactory<FilesDbContext>
{
    public FilesDbContext CreateDbContext(string[] args)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        services.AddKunEventBus();
        services.AddQianData<FilesDbContext>((_, options) => options.UseNpgsql("Host=localhost;Database=design;Username=design").UseKunSchema(FilesDbContext.Schema));
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<FilesDbContext>();
    }
}
