using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Registry;
using Qian.Testing.Containers;

namespace Qian.Lifecycle.Tests;

// S1-RT-005 验收 6：GitOps 执行器为 runtime.form: service 组件生成 Kustomize 片段并提交部署仓库，健康 = K8s Deployment 状态 + 组件 /health
// 双条件，结论回写修订与 Registry 制品目录。Argo 收敛（S1-DEP-005）在测试里以集群内 kubectl apply -k 代替，完整片段（资源、NetworkPolicy 等）归 S1-DEP-004。
public sealed partial class GitOpsExecutorTests(PostgresFixture postgres, K3sFixture k3s) : IClassFixture<PostgresFixture>, IClassFixture<K3sFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string Workload = "acme-echo-1-0-0";
    // 观测用的只读服务账号：只给目标命名空间 deployments 的 get 与 services/proxy 的 get。
    private const string Rbac = """
        apiVersion: v1
        kind: Namespace
        metadata: { name: qian-plugins }
        ---
        apiVersion: v1
        kind: ServiceAccount
        metadata: { name: qian-lifecycle, namespace: qian-plugins }
        ---
        apiVersion: rbac.authorization.k8s.io/v1
        kind: Role
        metadata: { name: qian-lifecycle-observer, namespace: qian-plugins }
        rules:
        - { apiGroups: ["apps"], resources: ["deployments"], verbs: ["get"] }
        - { apiGroups: [""], resources: ["services/proxy"], verbs: ["get"] }
        ---
        apiVersion: rbac.authorization.k8s.io/v1
        kind: RoleBinding
        metadata: { name: qian-lifecycle-observer, namespace: qian-plugins }
        subjects: [{ kind: ServiceAccount, name: qian-lifecycle, namespace: qian-plugins }]
        roleRef: { apiGroup: rbac.authorization.k8s.io, kind: Role, name: qian-lifecycle-observer }
        """;

    [Fact]
    public async Task GitOps执行器为service组件提交Kustomize片段_集群收敛后Deployment状态与health双条件回写注册表_工作负载不可用时回到Progressing()
    {
        using var repository = new DeployRepository();
        var server = ServerPattern().Match(k3s.Kubeconfig).Groups[1].Value;
        var authority = Encoding.UTF8.GetString(Convert.FromBase64String(AuthorityPattern().Match(k3s.Kubeconfig).Groups[1].Value));
        await k3s.CopyAsync(Rbac, "/tmp/qian-rbac.yaml", Ct);
        await k3s.KubectlAsync(Ct, "apply", "-f", "/tmp/qian-rbac.yaml");
        var token = (await k3s.KubectlAsync(Ct, "-n", "qian-plugins", "create", "token", "qian-lifecycle", "--duration=1h")).Trim();
        var config = new
        {
            executor = new
            {
                kind = "gitops", repository = repository.Bare, branch = "main", path = "apps/qian-plugins", @namespace = "qian-plugins",
                workingDirectory = repository.Work, healthTimeoutSeconds = 5,
                kubernetes = new { server, token = "secretRef://platform/lifecycle/k8s-token", certificateAuthority = "secretRef://platform/lifecycle/k8s-ca" }
            }
        };
        await using var host = await LifecycleHost.StartAsync(postgres, config, ct: Ct);
        host.WriteSecret("lifecycle-k8s", "token", token);
        host.WriteSecret("lifecycle-k8s", "ca", authority);
        var platform = new SigningKey();
        host.Trust(0, "k1", platform, host.Time.Now.AddDays(-1));
        var artifact = Artifacts.Signed(Artifacts.Service("acme.echo", "1.0.0"), platform);

        // 安装即提交期望状态：工作负载尚不存在，双条件都不满足，修订与制品目录记为 Progressing，不能激活。
        var installed = await host.Lifecycle.InstallAsync(artifact, "operator", Ct);
        Assert.Equal(("service", DeploymentHealthStates.Progressing), (installed.Form, installed.Health));
        var head = repository.Git("rev-parse", "main");
        Assert.Equal(head, installed.DeploymentReference);
        Assert.Equal(["README.md", "apps/qian-plugins/acme-echo-1-0-0/deployment.yaml", "apps/qian-plugins/acme-echo-1-0-0/kustomization.yaml",
            "apps/qian-plugins/acme-echo-1-0-0/service.yaml", "apps/qian-plugins/kustomization.yaml"],
            repository.Git("ls-tree", "-r", "--name-only", "main").Split('\n').Order(StringComparer.Ordinal).ToArray());
        var deployment = repository.Git("show", "main:apps/qian-plugins/acme-echo-1-0-0/deployment.yaml");
        Assert.Contains("image: \"" + Artifacts.Podinfo["oci://".Length..] + "\"", deployment, StringComparison.Ordinal);
        Assert.Contains("qian.dev/artifact-digest: \"" + artifact.Digest + "\"", deployment, StringComparison.Ordinal);
        Assert.Contains("containerPort: 9898", deployment, StringComparison.Ordinal);
        Assert.Contains("path: \"/healthz\"", deployment, StringComparison.Ordinal);
        Assert.Contains("namespace: \"qian-plugins\"", repository.Git("show", "main:apps/qian-plugins/acme-echo-1-0-0/kustomization.yaml"), StringComparison.Ordinal);
        Assert.Contains("- " + Workload, repository.Git("show", "main:apps/qian-plugins/kustomization.yaml"), StringComparison.Ordinal);
        Assert.Equal(RegistryArtifactStates.Progressing, await State(artifact.Digest));
        await SignatureTests.AssertCode(LifecycleErrors.StateConflict, () => host.Lifecycle.ActivateAsync("acme.echo", 1, "operator", Ct).AsTask());
        // 同一摘要重复安装返回已有修订，不产生新的提交。
        Assert.Equal(head, (await host.Lifecycle.InstallAsync(artifact, "operator", Ct)).DeploymentReference);
        Assert.Equal(head, repository.Git("rev-parse", "main"));

        // Argo 收敛：取部署仓库这一提交的片段，在集群内 kubectl apply -k。
        foreach (var file in repository.Git("ls-tree", "-r", "--name-only", head, "apps/qian-plugins").Split('\n'))
            await k3s.CopyAsync(repository.Git("show", head + ":" + file) + "\n", "/tmp/deploy/" + file, Ct);
        await k3s.KubectlAsync(Ct, "apply", "-k", "/tmp/deploy/apps/qian-plugins");
        var healthy = await Eventually(() => host.Lifecycle.RefreshDeploymentAsync("acme.echo", 1, Ct).AsTask(), view => view.Health == DeploymentHealthStates.Healthy, TimeSpan.FromMinutes(4));
        Assert.Equal(RegistryArtifactStates.Healthy, await State(artifact.Digest));
        Assert.Equal(PluginRevisionStates.Active, (await host.Lifecycle.ActivateAsync("acme.echo", 1, "operator", Ct)).State);
        Assert.Equal(head, healthy.DeploymentReference);

        // 工作负载缩到 0：Deployment 不再可用，服务代理也没有可用端点，重新观测回到 Progressing 并回写。
        await k3s.KubectlAsync(Ct, "-n", "qian-plugins", "scale", "deployment/" + Workload, "--replicas=0");
        await Eventually(() => host.Lifecycle.RefreshDeploymentAsync("acme.echo", 1, Ct).AsTask(), view => view.Health == DeploymentHealthStates.Progressing, TimeSpan.FromMinutes(1));
        Assert.Equal(RegistryArtifactStates.Progressing, await State(artifact.Digest));
    }

    private Task<string?> State(string digest) => LifecycleHost.ScalarAsync(postgres, $"SELECT state FROM plugin_qian_registry.plugin_artifacts WHERE digest = '{digest}'", Ct);

    internal static async Task<T> Eventually<T>(Func<Task<T>> read, Func<T, bool> done, TimeSpan limit)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            var value = await read();
            if (done(value)) return value;
            if (watch.Elapsed > limit) Assert.Fail($"{limit} 内未达到预期：{value}");
            await Task.Delay(TimeSpan.FromSeconds(2), Ct);
        }
    }

    [GeneratedRegex(@"server:\s*(https://\S+)")] private static partial Regex ServerPattern();
    [GeneratedRegex(@"certificate-authority-data:\s*(\S+)")] private static partial Regex AuthorityPattern();

    // 部署仓库：本机裸仓库，带一个初始提交（与真实部署仓库一样已有 main 分支）；Work 是执行器的工作副本目录。
    private sealed class DeployRepository : IDisposable
    {
        private readonly string _root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "qian-deploy-" + Guid.NewGuid().ToString("N"))).FullName;
        internal DeployRepository()
        {
            Run(null, "init", "-q", "--bare", "-b", "main", Bare);
            var seed = Path.Combine(_root, "seed");
            Run(null, "clone", "-q", Bare, seed);
            File.WriteAllText(Path.Combine(seed, "README.md"), "部署仓库\n");
            Run(seed, "add", "README.md");
            Run(seed, "-c", "user.name=test", "-c", "user.email=test@qian.invalid", "commit", "-q", "-m", "init");
            Run(seed, "push", "-q", "origin", "HEAD:refs/heads/main");
        }
        internal string Bare => Path.Combine(_root, "deploy.git");
        internal string Work => Path.Combine(_root, "work");
        internal string Git(params string[] arguments) => Run(null, ["--git-dir=" + Bare, .. arguments]);
        private static string Run(string? directory, params string[] arguments)
        {
            var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            if (directory is not null) { start.ArgumentList.Add("-C"); start.ArgumentList.Add(directory); }
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, "git " + string.Join(' ', arguments) + "：" + error);
            return output.Trim();
        }
        public void Dispose() { try { Directory.Delete(_root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
}

// 验收 6 的 Local 执行器：开发态写出与 GitOps 相同的片段到本机目录，健康 = 端点可连 + /health 通过，与 GitOps 的双条件同义。
public sealed class LocalExecutorTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Local执行器开发态等价_写出同样的片段_端点可连且health通过才健康_health失败或端点不可达都回到Progressing()
    {
        var status = StatusCodes.Status200OK;
        var web = await StartHealthServer(() => status);
        var state = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "qian-local-state-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var config = new { executor = new { kind = "local", stateDirectory = state, endpoints = new Dictionary<string, string> { ["acme.echo@1.0.0"] = web.Address }, healthTimeoutSeconds = 2 } };
            await using var host = await LifecycleHost.StartAsync(postgres, config, ct: Ct);
            var platform = new SigningKey();
            host.Trust(0, "k1", platform, host.Time.Now.AddDays(-1));
            var artifact = Artifacts.Signed(Artifacts.Service("acme.echo", "1.0.0"), platform);

            var installed = await host.Lifecycle.InstallAsync(artifact, "operator", Ct);
            Assert.Equal(DeploymentHealthStates.Healthy, installed.Health);
            Assert.StartsWith("sha256:", installed.DeploymentReference, StringComparison.Ordinal);
            // 与 GitOps 同一份片段（不写命名空间）：Deployment + Service + kustomization。
            Assert.Equal(["deployment.yaml", "kustomization.yaml", "service.yaml"],
                Directory.EnumerateFiles(Path.Combine(state, "acme-echo-1-0-0")).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray());
            var deployment = await File.ReadAllTextAsync(Path.Combine(state, "acme-echo-1-0-0", "deployment.yaml"), Ct);
            Assert.Contains("qian.dev/artifact-digest: \"" + artifact.Digest + "\"", deployment, StringComparison.Ordinal);
            Assert.DoesNotContain("namespace:", await File.ReadAllTextAsync(Path.Combine(state, "acme-echo-1-0-0", "kustomization.yaml"), Ct), StringComparison.Ordinal);
            Assert.Equal(RegistryArtifactStates.Healthy, await State(artifact.Digest));
            Assert.Equal(PluginRevisionStates.Active, (await host.Lifecycle.ActivateAsync("acme.echo", 1, "operator", Ct)).State);

            // 端点可连但 /health 失败：只满足一个条件，不算健康。
            status = StatusCodes.Status503ServiceUnavailable;
            Assert.Equal(DeploymentHealthStates.Progressing, (await host.Lifecycle.RefreshDeploymentAsync("acme.echo", 1, Ct)).Health);
            Assert.Equal(RegistryArtifactStates.Progressing, await State(artifact.Digest));
            status = StatusCodes.Status200OK;
            Assert.Equal(DeploymentHealthStates.Healthy, (await host.Lifecycle.RefreshDeploymentAsync("acme.echo", 1, Ct)).Health);
            // 端点关闭：工作负载条件也不满足。
            await web.App.DisposeAsync();
            Assert.Equal(DeploymentHealthStates.Progressing, (await host.Lifecycle.RefreshDeploymentAsync("acme.echo", 1, Ct)).Health);
            Assert.Equal(RegistryArtifactStates.Progressing, await State(artifact.Digest));
        }
        finally { await web.App.DisposeAsync(); Directory.Delete(state, true); }
    }

    [Fact]
    public async Task 没有执行器时service组件安装失败_module组件须随宿主镜像预装同一摘要_失败都不登记制品()
    {
        await using var host = await LifecycleHost.StartAsync(postgres, ct: Ct);
        var platform = new SigningKey();
        host.Trust(0, "k1", platform, host.Time.Now.AddDays(-1));
        await SignatureTests.AssertCode(LifecycleErrors.DeploymentUnavailable,
            () => host.Lifecycle.InstallAsync(Artifacts.Signed(Artifacts.Service("acme.echo", "1.0.0"), platform), "operator", Ct).AsTask());
        await SignatureTests.AssertCode(LifecycleErrors.RevisionNotDeployed, () => host.Lifecycle.InstallAsync(
            Artifacts.Signed(Artifacts.Declarative("acme.module", "1.0.0", new { form = "module", entry = "Acme.Module" }), platform), "operator", Ct).AsTask());
        Assert.Equal(0, await LifecycleHost.CountAsync(postgres, "SELECT count(*) FROM plugin_qian_registry.plugin_artifacts", Ct));
        Assert.Equal(0, await LifecycleHost.CountAsync(postgres, "SELECT count(*) FROM plugin_qian_lifecycle.plugin_revisions", Ct));
    }

    private Task<string?> State(string digest) => LifecycleHost.ScalarAsync(postgres, $"SELECT state FROM plugin_qian_registry.plugin_artifacts WHERE digest = '{digest}'", Ct);

    private static async Task<(WebApplication App, string Address)> StartHealthServer(Func<int> status)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
        var app = builder.Build();
        app.MapGet("/healthz", () => Results.StatusCode(status()));
        await app.StartAsync(Ct);
        return (app, app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
    }
}
