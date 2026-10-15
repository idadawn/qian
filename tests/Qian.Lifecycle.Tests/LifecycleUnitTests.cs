using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Qian.Runtime.Errors;

namespace Qian.Lifecycle.Tests;

// 不依赖容器的部分：包内迁移与 RLS、Kustomize 片段的确定性、工作负载命名、组件配置的严格解析、钩子幂等键。
public sealed class LifecycleUnitTests
{
    [Fact]
    public void 设计时入口经AddQianData建上下文_模型没有待生成的迁移_租户表带强制RLS_平台修订表与Outbox不带()
    {
        using var context = new LifecycleDesignTimeFactory().CreateDbContext([]);
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal(["20261015081937_LifecycleStateMachines"], context.Database.GetMigrations());
        var script = context.GetService<IMigrator>().GenerateScript();
        foreach (var table in new[] { "tenant_plugin_installs", "lifecycle_runs", "mount_failures" })
        {
            var name = LifecycleDbContext.Schema + "." + table;
            Assert.Contains("ALTER TABLE " + name + " FORCE ROW LEVEL SECURITY;", script, StringComparison.Ordinal);
            Assert.Contains("CREATE POLICY qian_tenant_guard ON " + name + " AS RESTRICTIVE FOR ALL", script, StringComparison.Ordinal);
        }
        foreach (var table in new[] { "plugin_revisions", "outbox_messages" })
            Assert.DoesNotContain("ALTER TABLE " + LifecycleDbContext.Schema + "." + table + " ENABLE ROW LEVEL SECURITY", script, StringComparison.Ordinal);
        // 同一组件至多一个 Active 修订由部分唯一索引兜住。
        Assert.Contains("CREATE UNIQUE INDEX ux_plugin_revisions_active ON plugin_qian_lifecycle.plugin_revisions (plugin_id) WHERE state = 'Active';", script, StringComparison.Ordinal);
    }

    [Fact]
    public void 工作负载名含版本且是DNS1123标签_超长时截断加摘要_片段逐字节确定()
    {
        Assert.Equal("acme-echo-1-0-0", KustomizeFragment.Workload("acme.echo", "1.0.0"));
        Assert.Equal("acme-echo-2-0-0-rc-1-build-7", KustomizeFragment.Workload("acme.echo", "2.0.0-rc.1+build.7"));
        var longName = KustomizeFragment.Workload("acme." + new string('x', 80), "1.0.0");
        Assert.True(longName.Length <= 63 && System.Text.RegularExpressions.Regex.IsMatch(longName, "^[a-z]([-a-z0-9]*[a-z0-9])?$"), longName);
        Assert.NotEqual(longName, KustomizeFragment.Workload("acme." + new string('x', 80), "1.0.1"));
        var request = new DeploymentRequest("acme.echo", "1.0.0", 3, "sha256:" + new string('a', 64), "ghcr.io/acme/echo@sha256:" + new string('b', 64), 9898, "/healthz");
        var first = KustomizeFragment.Render(request, "acme-echo-1-0-0", "qian-plugins");
        var second = KustomizeFragment.Render(request, "acme-echo-1-0-0", "qian-plugins");
        Assert.Equal(first, second);
        Assert.Equal(KustomizeFragment.Fingerprint(first), KustomizeFragment.Fingerprint(second));
        Assert.Contains("qian.dev/revision: \"3\"", first["deployment.yaml"], StringComparison.Ordinal);
        Assert.Contains("drop: [\"ALL\"]", first["deployment.yaml"], StringComparison.Ordinal);
        Assert.Equal(["acme-echo-1-0-0", "zeta-1-0-0"], KustomizeFragment.RootResources(KustomizeFragment.Root(["zeta-1-0-0", "acme-echo-1-0-0", "zeta-1-0-0"])));
    }

    [Theory]
    [InlineData("""{"unknown":1}""", "#/unknown")]
    [InlineData("""{"runLeaseSeconds":5}""", "#/runLeaseSeconds")]
    [InlineData("""{"executor":{"kind":"argo"}}""", "#/executor/kind")]
    [InlineData("""{"executor":{"kind":"gitops","repository":"https://user:pass@git.example/deploy.git","kubernetes":{"server":"https://k8s.example","token":"secretRef://platform/k8s/token"}}}""", "#/executor/repository")]
    [InlineData("""{"executor":{"kind":"gitops","repository":"relative/path","kubernetes":{"server":"https://k8s.example","token":"secretRef://platform/k8s/token"}}}""", "#/executor/repository")]
    [InlineData("""{"executor":{"kind":"gitops","repository":"https://git.example/deploy.git","kubernetes":{"server":"http://k8s.example","token":"secretRef://platform/k8s/token"}}}""", "#/executor/kubernetes/server")]
    [InlineData("""{"executor":{"kind":"gitops","repository":"https://git.example/deploy.git","kubernetes":{"server":"https://k8s.example","token":"plain-token"}}}""", "#/executor/kubernetes/token")]
    [InlineData("""{"executor":{"kind":"gitops","repository":"https://git.example/deploy.git","path":"../escape","kubernetes":{"server":"https://k8s.example","token":"secretRef://platform/k8s/token"}}}""", "#/executor/path")]
    [InlineData("""{"executor":{"kind":"local","stateDirectory":"/tmp/state","endpoints":{"acme.echo":"http://127.0.0.1:8080/health"}}}""", "#/executor/endpoints/acme.echo")]
    [InlineData("""{"executor":{"kind":"local"}}""", "#/executor/stateDirectory")]
    public void 组件配置严格解析_未知字段明文令牌内嵌凭据与越界路径都拒绝(string config, string location)
    {
        var error = Assert.Throws<QianErrorException>(() => LifecycleSettings.Parse(JsonSerializer.Deserialize<JsonElement>(config)));
        Assert.Equal(PlatformErrors.InputInvalid.Code, error.Error.Code.Code);
        Assert.Equal("plugins/qian.lifecycle/config" + location, Assert.Single(error.Error.Errors).Pointer);
    }

    [Fact]
    public void 空配置不带执行器_钩子幂等键由租户组件钩子与目标版本确定且是第8版UUID()
    {
        Assert.Null(LifecycleSettings.Parse(JsonSerializer.Deserialize<JsonElement>("{}")).Executor);
        var key = LifecycleController.IdempotencyKey("01900000-0000-7000-8000-000000000001", "qian.sample.crm", LifecycleHooks.TenantProvision, "1.0.0");
        Assert.Equal(key, LifecycleController.IdempotencyKey("01900000-0000-7000-8000-000000000001", "qian.sample.crm", LifecycleHooks.TenantProvision, "1.0.0"));
        Assert.NotEqual(key, LifecycleController.IdempotencyKey("01900000-0000-7000-8000-000000000001", "qian.sample.crm", LifecycleHooks.TenantProvision, "1.0.1"));
        Assert.NotEqual(key, LifecycleController.IdempotencyKey("01900000-0000-7000-8000-000000000002", "qian.sample.crm", LifecycleHooks.TenantProvision, "1.0.0"));
        Assert.Equal(8, key.Version);
    }
}
