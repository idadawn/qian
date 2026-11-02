using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;

namespace Qian.Tenancy.Signup.Tests;

// dotnet ef migrations add <名称> --project packages/Qian.Tenancy.Signup --startup-project tests/Qian.Tenancy.Signup.Tests
//   --context TenancySignupDbContext --output-dir Migrations
// 与仓库现有迁移一样生成为 internal；生成后不手改迁移或 SQL。
internal sealed class SignupDesignTimeFactory : IDesignTimeDbContextFactory<TenancySignupDbContext>
{
    public TenancySignupDbContext CreateDbContext(string[] args)
    {
        var services = new ServiceCollection(); services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext()); services.AddKunEventBus();
        services.AddQianData<TenancySignupDbContext>((_, options) => options.UseNpgsql("Host=localhost;Database=design;Username=design").UseKunSchema(TenancySignupDbContext.Schema));
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<TenancySignupDbContext>();
    }
}

public sealed class SignupDesignTimeServices : IDesignTimeServices
{
    public void ConfigureDesignTimeServices(IServiceCollection serviceCollection) => serviceCollection.AddSingleton<IMigrationsCodeGenerator, InternalMigrationsGenerator>();
}

internal sealed class InternalMigrationsGenerator(MigrationsCodeGeneratorDependencies dependencies, CSharpMigrationsGeneratorDependencies csharp)
    : CSharpMigrationsGenerator(dependencies, csharp)
{
    public override string GenerateMigration(string? migrationNamespace, string migrationName, IReadOnlyList<MigrationOperation> upOperations, IReadOnlyList<MigrationOperation> downOperations)
        => base.GenerateMigration(migrationNamespace, migrationName, upOperations, downOperations).Replace("public partial class " + migrationName, "internal partial class " + migrationName, StringComparison.Ordinal);
}
