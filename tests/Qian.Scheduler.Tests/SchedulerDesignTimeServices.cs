using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace Qian.Scheduler.Tests;

// 与仓库现有迁移的 internal 可见性一致；通过 EF 设计时生成定制完成，生成后不手改迁移或 SQL。
public sealed class SchedulerDesignTimeServices : IDesignTimeServices
{
    public void ConfigureDesignTimeServices(IServiceCollection serviceCollection) => serviceCollection.AddSingleton<IMigrationsCodeGenerator, InternalMigrationsGenerator>();
}
internal sealed class InternalMigrationsGenerator(MigrationsCodeGeneratorDependencies dependencies, CSharpMigrationsGeneratorDependencies csharp)
    : CSharpMigrationsGenerator(dependencies, csharp)
{
    public override string GenerateMigration(string? migrationNamespace, string migrationName, IReadOnlyList<MigrationOperation> upOperations, IReadOnlyList<MigrationOperation> downOperations)
        => base.GenerateMigration(migrationNamespace, migrationName, upOperations, downOperations).Replace("public partial class " + migrationName, "internal partial class " + migrationName, StringComparison.Ordinal);
}
