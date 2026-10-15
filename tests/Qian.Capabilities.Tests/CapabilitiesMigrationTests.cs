using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Qian.Capabilities.Tests;

// S1-RT-020 第 3 项：设计时入口与运行时同一装配，模型与包内迁移一致；RLS 与策略在迁移执行期由 Qian 迁移生成器补出，不在迁移文件里。
public sealed class CapabilitiesMigrationTests
{
    [Fact]
    public void 设计时入口经AddQianData建上下文_模型没有待生成的迁移_迁移脚本带强制RLS与两条租户策略()
    {
        using var context = new CapabilitiesDesignTimeFactory().CreateDbContext([]);
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal(["20261014100230_NavigationAndInvokeReceipts", "20261014170738_InvokeReceiptLimits"], context.Database.GetMigrations());
        var script = context.GetService<IMigrator>().GenerateScript();
        foreach (var table in new[] { "navigation_preferences", "invoke_receipts" })
        {
            var name = CapabilitiesDbContext.Schema + "." + table;
            Assert.Contains("ALTER TABLE " + name + " ENABLE ROW LEVEL SECURITY;", script, StringComparison.Ordinal);
            Assert.Contains("ALTER TABLE " + name + " FORCE ROW LEVEL SECURITY;", script, StringComparison.Ordinal);
            Assert.Contains("CREATE POLICY qian_tenant_access ON " + name + " FOR ALL USING (true) WITH CHECK (true);", script, StringComparison.Ordinal);
            Assert.Contains("CREATE POLICY qian_tenant_guard ON " + name + " AS RESTRICTIVE FOR ALL", script, StringComparison.Ordinal);
        }
        // Outbox 表的租户列可空，不是租户表，不加策略；迁移文件本身不含任何 RLS 语句。
        Assert.DoesNotContain("ALTER TABLE " + CapabilitiesDbContext.Schema + ".outbox_messages ENABLE ROW LEVEL SECURITY", script, StringComparison.Ordinal);
        Assert.DoesNotContain("POLICY qian_tenant_guard ON " + CapabilitiesDbContext.Schema + ".outbox_messages", script, StringComparison.Ordinal);
        Assert.Contains("ck_invoke_receipts_response_size", script, StringComparison.Ordinal);
        var sources = Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "packages/Qian.Capabilities/Migrations"), "*.cs");
        Assert.All(sources, file => Assert.DoesNotContain("ROW LEVEL SECURITY", File.ReadAllText(file), StringComparison.Ordinal));
    }

    private static string RepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        return root?.FullName ?? throw new InvalidOperationException("找不到仓库根目录。");
    }
}
