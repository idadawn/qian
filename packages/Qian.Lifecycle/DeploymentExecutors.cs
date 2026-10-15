using System.Diagnostics;
using System.Globalization;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Qian.Config;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Lifecycle;

// GitOps 执行器：把 Kustomize 片段提交到部署仓库（Argo 收敛，S1-DEP-005），健康 = K8s Deployment 状态 + 经 API Server 服务代理访问组件 /health。
// 仓库凭据由宿主环境的 git 凭据助手提供；K8s 令牌每次观测重新从秘密解析，轮换不重启。
internal sealed class GitOpsDeploymentExecutor(GitOpsSettings settings, IPluginContext context) : IDeploymentExecutor, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _workingDirectory = settings.WorkingDirectory ?? Path.Combine(Path.GetTempPath(), "qian-lifecycle-gitops",
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(settings.Repository + "\n" + settings.Branch)))[..16]);
    private HttpClient? _client;

    public string Name => "gitops";

    public async ValueTask<DeploymentHandle> ApplyAsync(DeploymentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var workload = KustomizeFragment.Workload(request.PluginId, request.Version);
        var files = KustomizeFragment.Render(request, workload, settings.Namespace);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 推送被拒（他人先推进了分支）时重新取回再提交；同一片段已在分支上时不产生新提交。
            for (var attempt = 0; ; attempt++)
            {
                await SynchronizeAsync(cancellationToken).ConfigureAwait(false);
                var root = Path.Combine(_workingDirectory, settings.Path);
                Directory.CreateDirectory(Path.Combine(root, workload));
                foreach (var file in files) await File.WriteAllTextAsync(Path.Combine(root, workload, file.Key), file.Value, cancellationToken).ConfigureAwait(false);
                var index = Path.Combine(root, "kustomization.yaml");
                var existing = File.Exists(index) ? KustomizeFragment.RootResources(await File.ReadAllTextAsync(index, cancellationToken).ConfigureAwait(false)) : [];
                await File.WriteAllTextAsync(index, KustomizeFragment.Root(existing.Append(workload)), cancellationToken).ConfigureAwait(false);
                await Git(cancellationToken, "add", "-A", "--", settings.Path).ConfigureAwait(false);
                if ((await Run(cancellationToken, "diff", "--cached", "--quiet").ConfigureAwait(false)).Code != 0)
                {
                    await Git(cancellationToken, "-c", "user.name=qian.lifecycle", "-c", "user.email=lifecycle@qian.invalid", "commit", "-q", "-m",
                        $"qian.lifecycle: deploy {request.PluginId}@{request.Version} (revision {request.Revision.ToString(CultureInfo.InvariantCulture)})").ConfigureAwait(false);
                    var push = await Run(cancellationToken, "push", "-q", "origin", "HEAD:refs/heads/" + settings.Branch).ConfigureAwait(false);
                    if (push.Code != 0)
                    {
                        LifecycleLog.PushRejected(context.Logger, attempt + 1, push.Error);
                        if (attempt < 2) continue;
                        throw Unavailable("部署仓库推送失败。");
                    }
                }
                var head = await Git(cancellationToken, "rev-parse", "HEAD").ConfigureAwait(false);
                return new DeploymentHandle(Name, request.PluginId, request.Version, workload, head, request.Port, request.HealthPath);
            }
        }
        finally { _gate.Release(); }
    }

    public async ValueTask<DeploymentHealth> ObserveAsync(DeploymentHandle handle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handle);
        bool ready = false, healthy = false; string workloadDetail, healthDetail;
        try
        {
            var client = await ClientAsync(cancellationToken).ConfigureAwait(false);
            using var token = await context.ResolveSecretAsync(settings.Token, cancellationToken).ConfigureAwait(false);
            var bearer = token.RevealUtf8().Trim();
            using (var response = await Send(client, bearer, $"/apis/apps/v1/namespaces/{settings.Namespace}/deployments/{handle.Workload}", cancellationToken).ConfigureAwait(false))
            {
                if (response.IsSuccessStatusCode)
                {
                    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
                    (ready, workloadDetail) = Judge(document.RootElement, handle.Version);
                }
                else workloadDetail = "deployment HTTP " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
            }
            // API Server 的服务代理：不要求 Lifecycle 进入集群网络，也只经过已授权的 services/proxy。
            var proxy = $"/api/v1/namespaces/{settings.Namespace}/services/{handle.Workload}:{handle.Port.ToString(CultureInfo.InvariantCulture)}/proxy{handle.HealthPath}";
            using (var response = await Send(client, bearer, proxy, cancellationToken).ConfigureAwait(false))
            {
                healthy = response.IsSuccessStatusCode;
                healthDetail = handle.HealthPath + " HTTP " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
            }
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException or QianErrorException or IOException && !cancellationToken.IsCancellationRequested)
        {
            LifecycleLog.ObserveFailed(context.Logger, error, handle.Workload);
            return new DeploymentHealth(ready, false, "k8s api unreachable");
        }
        return new DeploymentHealth(ready, healthy, workloadDetail + "; " + healthDetail);
    }

    // 就绪 = 控制器已观测到当前代际、全部副本已更新且可用，并且工作负载就是这个版本（注解对得上）。
    private static (bool Ready, string Detail) Judge(JsonElement deployment, string version)
    {
        static long Number(JsonElement element, params string[] path)
        {
            foreach (var name in path) if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out element)) return 0;
            return element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var value) ? value : 0;
        }
        var replicas = deployment.TryGetProperty("spec", out var spec) && spec.TryGetProperty("replicas", out _) ? Number(deployment, "spec", "replicas") : 1;
        var available = Number(deployment, "status", "availableReplicas");
        var annotated = deployment.TryGetProperty("metadata", out var metadata) && metadata.TryGetProperty("annotations", out var annotations) &&
            annotations.ValueKind == JsonValueKind.Object && annotations.TryGetProperty("qian.dev/version", out var value) && value.ValueKind == JsonValueKind.String && value.GetString() == version;
        var ready = replicas >= 1 && annotated && Number(deployment, "status", "observedGeneration") >= Number(deployment, "metadata", "generation") &&
            Number(deployment, "status", "updatedReplicas") >= replicas && available >= replicas;
        return (ready, $"deployment {available.ToString(CultureInfo.InvariantCulture)}/{replicas.ToString(CultureInfo.InvariantCulture)} available");
    }

    private async Task<HttpResponseMessage> Send(HttpClient client, string bearer, string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(settings.Server, path));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearer);
        return await client.SendAsync(request, ct).ConfigureAwait(false);
    }

    // 配置了集群 CA 时只信任它签发的证书链（主机名仍须匹配）；否则使用系统信任。
    private async ValueTask<HttpClient> ClientAsync(CancellationToken ct)
    {
        if (Volatile.Read(ref _client) is { } existing) return existing;
        X509Certificate2? authority = null;
        if (settings.CertificateAuthority is { } reference)
        {
            using var secret = await context.ResolveSecretAsync(reference, ct).ConfigureAwait(false);
            authority = X509Certificate2.CreateFromPem(secret.RevealUtf8());
        }
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false, ConnectTimeout = settings.HealthTimeout };
        if (authority is not null)
            handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
            {
                if (certificate is null || (errors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0) return false;
                using var chain = new X509Chain();
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(authority);
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                return chain.Build(certificate as X509Certificate2 ?? X509CertificateLoader.LoadCertificate(certificate.GetRawCertData()));
            };
        var client = new HttpClient(handler) { Timeout = settings.HealthTimeout };
        if (Interlocked.CompareExchange(ref _client, client, null) is { } raced) { client.Dispose(); return raced; }
        return client;
    }

    // 工作副本只属于本执行器：不存在就浅克隆目标分支，存在就硬重置到远端最新，避免上一次失败残留。
    private async Task SynchronizeAsync(CancellationToken ct)
    {
        if (!Directory.Exists(Path.Combine(_workingDirectory, ".git")))
        {
            if (Directory.Exists(_workingDirectory)) Directory.Delete(_workingDirectory, true);
            Directory.CreateDirectory(Path.GetDirectoryName(_workingDirectory)!);
            var clone = await Process(null, ct, "clone", "-q", "--branch", settings.Branch, "--single-branch", "--", settings.Repository, _workingDirectory).ConfigureAwait(false);
            if (clone.Code != 0)
            {
                LifecycleLog.CloneFailed(context.Logger, clone.Error);
                throw Unavailable("部署仓库无法克隆。");
            }
            return;
        }
        await Git(ct, "fetch", "-q", "origin", settings.Branch).ConfigureAwait(false);
        await Git(ct, "reset", "-q", "--hard", "FETCH_HEAD").ConfigureAwait(false);
        await Git(ct, "clean", "-q", "-fdx").ConfigureAwait(false);
    }

    private async Task<string> Git(CancellationToken ct, params string[] arguments)
    {
        var result = await Run(ct, arguments).ConfigureAwait(false);
        if (result.Code == 0) return result.Output;
        LifecycleLog.GitFailed(context.Logger, arguments.FirstOrDefault(argument => !argument.StartsWith('-') && !argument.Contains('=', StringComparison.Ordinal)), result.Error);
        throw Unavailable("部署仓库操作失败。");
    }

    private Task<(int Code, string Output, string Error)> Run(CancellationToken ct, params string[] arguments) => Process(_workingDirectory, ct, arguments);

    // 直接执行 git（不经 shell），关闭交互式凭据提示；单条命令最长两分钟。
    private static async Task<(int Code, string Output, string Error)> Process(string? directory, CancellationToken ct, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        if (directory is not null) { start.ArgumentList.Add("-C"); start.ArgumentList.Add(directory); }
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0"; start.Environment["LC_ALL"] = "C";
        using var process = System.Diagnostics.Process.Start(start) ?? throw Unavailable("git 不可用。");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch (InvalidOperationException) { }
            if (ct.IsCancellationRequested) throw;
            throw Unavailable("部署仓库操作超时。");
        }
        return (process.ExitCode, (await output.ConfigureAwait(false)).Trim(), (await error.ConfigureAwait(false)).Trim());
    }

    internal static QianErrorException Unavailable(string reason) =>
        new(new QianError(LifecycleErrors.DeploymentUnavailable, new Dictionary<string, string> { ["reason"] = reason }));

    public void Dispose() { Interlocked.Exchange(ref _client, null)?.Dispose(); _gate.Dispose(); }
}

// 开发态执行器：把同样的片段写到本机期望状态目录（供本机编排工具读取），健康 = 端点 TCP 可连 + /health 通过，与 GitOps 的双条件同义。
internal sealed class LocalDeploymentExecutor(LocalSettings settings) : IDeploymentExecutor, IDisposable
{
    private readonly HttpClient _client = new(new SocketsHttpHandler { AllowAutoRedirect = false, ConnectTimeout = settings.HealthTimeout }) { Timeout = settings.HealthTimeout };

    public string Name => "local";

    public async ValueTask<DeploymentHandle> ApplyAsync(DeploymentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var workload = KustomizeFragment.Workload(request.PluginId, request.Version);
        var files = KustomizeFragment.Render(request, workload, null);
        var directory = Path.Combine(settings.StateDirectory, workload);
        Directory.CreateDirectory(directory);
        foreach (var file in files) await File.WriteAllTextAsync(Path.Combine(directory, file.Key), file.Value, cancellationToken).ConfigureAwait(false);
        return new DeploymentHandle(Name, request.PluginId, request.Version, workload, KustomizeFragment.Fingerprint(files), request.Port, request.HealthPath);
    }

    public async ValueTask<DeploymentHealth> ObserveAsync(DeploymentHandle handle, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handle);
        var endpoint = settings.Endpoints.GetValueOrDefault(handle.PluginId + "@" + handle.Version) ?? settings.Endpoints.GetValueOrDefault(handle.PluginId)
            ?? new Uri("http://127.0.0.1:" + handle.Port.ToString(CultureInfo.InvariantCulture) + "/");
        var ready = false;
        try
        {
            using var socket = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(settings.HealthTimeout);
            await socket.ConnectAsync(endpoint.Host, endpoint.Port, timeout.Token).ConfigureAwait(false);
            ready = true;
        }
        catch (Exception error) when (error is SocketException or OperationCanceledException && !cancellationToken.IsCancellationRequested) { }
        if (!ready) return new DeploymentHealth(false, false, "endpoint unreachable");
        try
        {
            using var response = await _client.GetAsync(new Uri(endpoint, handle.HealthPath), cancellationToken).ConfigureAwait(false);
            return new DeploymentHealth(true, response.IsSuccessStatusCode, "endpoint reachable; " + handle.HealthPath + " HTTP " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return new DeploymentHealth(true, false, "endpoint reachable; " + handle.HealthPath + " unreachable");
        }
    }

    public void Dispose() => _client.Dispose();
}
