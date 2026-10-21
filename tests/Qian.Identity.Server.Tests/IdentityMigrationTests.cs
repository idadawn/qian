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
        Assert.False(context.Database.HasPendingModelChanges()); Assert.Equal(2, context.Database.GetMigrations().Count());
        var script = context.GetService<IMigrator>().GenerateScript();
        Assert.Contains("plugin_qian_identity", script, StringComparison.Ordinal);
        Assert.Contains("logout_deliveries", script, StringComparison.Ordinal);
        Assert.Contains("open_iddict_tokens", script, StringComparison.Ordinal);
    }

    [Fact]
    public void 账号模型表齐全且只有租户表启用行级安全()
    {
        using var context = new IdentityDesignTimeFactory().CreateDbContext([]);
        var script = context.GetService<IMigrator>().GenerateScript();
        foreach (var table in new[] { "persons", "person_identifiers", "password_history", "memberships", "external_identities", "clients", "sessions", "login_audits", "login_tickets", "login_captchas" })
            Assert.Contains($"CREATE TABLE plugin_qian_identity.{table} (", script, StringComparison.Ordinal);
        // Membership 与外部身份是租户表：强制 RLS，且有宽松放行 + 严格租户守卫两条策略；Person 及其凭据、会话、审计是全局表，不启用 RLS。
        foreach (var table in new[] { "memberships", "external_identities" })
        {
            Assert.Contains($"ALTER TABLE plugin_qian_identity.{table} FORCE ROW LEVEL SECURITY;", script, StringComparison.Ordinal);
            Assert.Contains($"CREATE POLICY qian_tenant_guard ON plugin_qian_identity.{table} AS RESTRICTIVE", script, StringComparison.Ordinal);
        }
        Assert.Equal(2, script.Split("FORCE ROW LEVEL SECURITY", StringSplitOptions.None).Length - 1);
        // 手机、邮箱、用户名唯一且解绑（软删除）后释放；同一人在同一租户只有一个成员身份。
        Assert.Contains("ix_person_identifiers_kind_value", script, StringComparison.Ordinal);
        Assert.Contains("ix_memberships_tenant_id_person_id", script, StringComparison.Ordinal);
        Assert.Contains("ix_external_identities_tenant_id_connector_id_subject", script, StringComparison.Ordinal);
    }
}
