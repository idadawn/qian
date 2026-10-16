using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Qian.Identity.Server.Tests;

public sealed class IdentityMigrationTests
{
    [Fact]
    public void 协议持久表和后通道队列有完整迁移且模型无漂移()
    {
        using var context = new IdentityDesignTimeFactory().CreateDbContext([]);
        Assert.False(context.Database.HasPendingModelChanges()); Assert.Single(context.Database.GetMigrations());
        var script = context.GetService<IMigrator>().GenerateScript();
        Assert.Contains("plugin_qian_identity", script, StringComparison.Ordinal);
        Assert.Contains("logout_deliveries", script, StringComparison.Ordinal);
        Assert.Contains("open_iddict_tokens", script, StringComparison.Ordinal);
    }
}
