using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using Kun.Core.MultiTenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qian.Config.Apollo;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Config.Secrets.Tests;

public sealed partial class SecretResolverTests
{
    [Theory]
    [InlineData("secretRef://platform/pg/password", "platform", "pg/password")]
    [InlineData("secretRef://tenant/a/db/token", "tenant", "a/db/token")]
    [InlineData("secretRef://dev/app/p%77", "dev", "app/pw")]
    public void 引用按规范解析(string text, string scope, string path)
    {
        var reference = SecretReference.Parse(text);
        Assert.Equal(scope, reference.Scope); Assert.Equal(path, reference.Path);
    }

    [Theory]
    [InlineData("secretRef://platform/a/../b")]
    [InlineData("secretRef://platform/a/%2e%2e/b")]
    [InlineData("secretRef://platform/a%2fb")]
    [InlineData("secretRef://platform/a?token=value")]
    [InlineData("secretRef://user:password@platform/a")]
    [InlineData("secretRef://k8s/ns/name#key")]
    [InlineData("secretRef://platform/")]
    public void 公开引用不接受穿越和物理定位(string text) => Assert.False(SecretReference.IsValid(text));

    [Fact]
    public void 值默认脱敏_不能序列化_释放后缓冲归零()
    {
        using var value = new SecretValue(Encoding.UTF8.GetBytes("synthetic-sensitive-value"));
        Assert.DoesNotContain("synthetic", value.ToString(), StringComparison.Ordinal);
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Serialize(value));
        var bytes = value.Bytes;
        Assert.Equal("synthetic-sensitive-value", value.RevealUtf8());
        value.Dispose();
        Assert.All(bytes.ToArray(), item => Assert.Equal(0, item));
        Assert.Throws<ObjectDisposedException>(() => value.RevealUtf8());
    }

    [Fact]
    public async Task K8s定位读取真实投射文件_跟随原子轮换_拒绝根外链接()
    {
        using var files = new Files();
        var directory = Directory.CreateDirectory(Path.Combine(files.Root, "qian", "database")).FullName;
        Directory.CreateDirectory(Path.Combine(directory, "version-one"));
        Directory.CreateDirectory(Path.Combine(directory, "version-two"));
        await File.WriteAllTextAsync(Path.Combine(directory, "version-one", "password"), "synthetic-first", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "version-two", "password"), "synthetic-second", TestContext.Current.CancellationToken);
        Directory.CreateSymbolicLink(Path.Combine(directory, "..data"), "version-one");
        File.CreateSymbolicLink(Path.Combine(directory, "password"), "..data/password");
        var location = KubernetesSecretLocation.Parse("secretRef://k8s/qian/database#password");
        var store = new MountedSecretStore(files.Root);
        using (var first = await store.ReadAsync(location, TestContext.Current.CancellationToken)) Assert.Equal("synthetic-first", first.RevealUtf8());
        Directory.CreateSymbolicLink(Path.Combine(directory, "..new"), "version-two");
        Assert.Equal(0, Rename(Path.Combine(directory, "..new"), Path.Combine(directory, "..data")));
        using (var second = await store.ReadAsync(location, TestContext.Current.CancellationToken)) Assert.Equal("synthetic-second", second.RevealUtf8());
        Directory.Delete(Path.Combine(directory, "..data"));
        Directory.CreateSymbolicLink(Path.Combine(directory, "..data"), files.Outside);
        await File.WriteAllTextAsync(Path.Combine(files.Outside, "password"), "outside-must-not-be-read", TestContext.Current.CancellationToken);
        var denied = await Assert.ThrowsAsync<QianErrorException>(async () => await store.ReadAsync(location, TestContext.Current.CancellationToken));
        Assert.Equal("SECRET_ACCESS_DENIED", denied.Error.Code.Code);
        Assert.DoesNotContain("outside-must", denied.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 声明和审批取交集_跨租户拒绝_审计不含值()
    {
        using var files = new Files();
        files.Write("a", "synthetic-a"); files.Write("b", "synthetic-b");
        var observer = new Observer();
        var factory = new SecretResolverFactory(Options(files.Root), observer);
        var tenant = new Tenant { TenantId = "a" };
        var resolver = factory.ForHost("qian.consumer", tenant);
        using (var value = await resolver.ResolveAsync("secretRef://tenant/db/token", TestContext.Current.CancellationToken)) Assert.Equal("synthetic-a", value.RevealUtf8());
        var denied = await Assert.ThrowsAsync<QianErrorException>(async () => await resolver.ResolveAsync("secretRef://tenant/b/db/token", TestContext.Current.CancellationToken));
        Assert.Equal("SECRET_ACCESS_DENIED", denied.Error.Code.Code);
        var unapproved = factory.ForHost("qian.declared", tenant);
        await Assert.ThrowsAsync<QianErrorException>(async () => await unapproved.ResolveAsync("secretRef://tenant/a/db/token", TestContext.Current.CancellationToken));
        tenant.TenantId = "b";
        using (var value = await resolver.ResolveAsync("secretRef://tenant/db/token", TestContext.Current.CancellationToken)) Assert.Equal("synthetic-b", value.RevealUtf8());
        Assert.Equal(4, observer.Events.Count);
        Assert.Contains(observer.Events, item => !item.Allowed && item.Outcome == "SECRET_ACCESS_DENIED");
        Assert.DoesNotContain("synthetic", JsonSerializer.Serialize(observer.Events), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 文件缺失和审计失败均返回结构化错误_不会给出空密钥()
    {
        using var files = new Files();
        var tenant = new Tenant { TenantId = "a" };
        var observer = new Observer();
        var resolver = new SecretResolverFactory(Options(files.Root), observer).ForHost("qian.consumer", tenant);
        var missing = await Assert.ThrowsAsync<QianErrorException>(async () => await resolver.ResolveAsync("secretRef://tenant/a/db/token", TestContext.Current.CancellationToken));
        Assert.Equal("SECRET_NOT_FOUND", missing.Error.Code.Code);
        files.Write("a", "synthetic-secret");
        var auditFailure = new SecretResolverFactory(Options(files.Root), new FailingObserver()).ForHost("qian.consumer", tenant);
        var error = await Assert.ThrowsAsync<QianErrorException>(async () => await auditFailure.ResolveAsync("secretRef://tenant/a/db/token", TestContext.Current.CancellationToken));
        Assert.Equal("SECRET_UNAVAILABLE", error.Error.Code.Code);
        Assert.DoesNotContain("synthetic-secret", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Config_seam使用插件身份_卸载后缓存的解析器不能再读()
    {
        using var files = new Files(); files.Write("a", "synthetic-secret");
        var tenant = new Tenant { TenantId = "a" };
        using var root = new ConfigurationRoot([]);
        using var services = new ServiceCollection().AddSingleton<ITenantContext>(tenant)
            .AddSingleton<ISecretAccessObserver>(new Observer())
            .AddQianSecrets(Options(files.Root)).AddQianConfiguration(root).BuildServiceProvider();
        await using var runtime = new PluginLoader(services);
        var config = JsonSerializer.SerializeToElement(new { });
        var scope = new ScopeKey("tenant", "a");
        await runtime.MountAsync([
            new PluginDefinition(new PluginEntry("config", "qian.config", "1", ScopeKey.Global, 0, config), new ConfigurationPlugin(), [ConfigDefinition.Key]),
            new PluginDefinition(new PluginEntry("consumer", "qian.consumer", "1", scope, 0, config), new Consumer(), [])
        ], TestContext.Current.CancellationToken);
        var context = runtime.GetContext("consumer", scope);
        using (var value = await context.ResolveSecretAsync("secretRef://tenant/db/token", TestContext.Current.CancellationToken)) Assert.Equal("synthetic-secret", value.RevealUtf8());
        var saved = context.Config().Secrets!.ForPlugin(context);
        await runtime.UnmountAsync("config", ScopeKey.Global, TestContext.Current.CancellationToken);
        var denied = await Assert.ThrowsAsync<QianErrorException>(async () => await saved.ResolveAsync("secretRef://tenant/db/token", TestContext.Current.CancellationToken));
        Assert.Equal("SECRET_ACCESS_DENIED", denied.Error.Code.Code);
    }

    private static SecretResolverOptions Options(string root) => new()
    {
        MountRoot = root,
        Bindings = [new("secretRef://tenant/a/db/token", new("qian", "a", "token"), "a"), new("secretRef://tenant/b/db/token", new("qian", "b", "token"), "b")],
        Policies = [new("qian.consumer", [new("db/*", "tenant")], [new("db/*", "tenant")]), new("qian.declared", [new("db/*", "tenant")], [])]
    };
    [Fact]
    public async Task 开发引用默认关闭_声明副本不可事后扩大_没有审计不能装配()
    {
        using var files = new Files(); files.Write("a", "synthetic-dev");
        SecretGrant[] declared = [];
        var options = new SecretResolverOptions
        {
            MountRoot = files.Root,
            Bindings = [new("secretRef://dev/app/token", new("qian", "a", "token"))],
            Policies = [new("qian.consumer", [new("app/*", "dev")], [new("app/*", "dev")])]
        };
        var denied = await Assert.ThrowsAsync<QianErrorException>(async () => await new SecretResolverFactory(options, new Observer())
            .ForHost("qian.consumer").ResolveAsync("secretRef://dev/app/token", TestContext.Current.CancellationToken));
        Assert.Equal("SECRET_ACCESS_DENIED", denied.Error.Code.Code);
        var grants = new List<SecretGrant>(declared);
        var factory = new SecretResolverFactory(new SecretResolverOptions
        {
            MountRoot = files.Root, AllowDevelopmentReferences = true, Bindings = options.Bindings,
            Policies = [new("qian.consumer", grants, [new("app/*", "dev")])]
        }, new Observer());
        grants.Add(new("app/*", "dev"));
        await Assert.ThrowsAsync<QianErrorException>(async () => await factory.ForHost("qian.consumer")
            .ResolveAsync("secretRef://dev/app/token", TestContext.Current.CancellationToken));
        using var services = new ServiceCollection().AddQianSecrets(options).BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(() => services.GetRequiredService<ISecretResolverFactory>());
    }
    // 模拟 K8s AtomicWriter 的 rename；File.Move 不支持覆盖目录符号链接。
    [LibraryImport("libc", EntryPoint = "rename", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int Rename(string source, string destination);
    private sealed class Tenant : ITenantContext { public string? TenantId { get; set; } public bool IsMultiTenant => true; }
    private sealed class Consumer : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [ConfigDefinition.Key];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => ValueTask.CompletedTask;
    }
    private sealed class Observer : ISecretAccessObserver
    {
        public List<SecretAccessEvent> Events { get; } = [];
        public ValueTask RecordAsync(SecretAccessEvent access, CancellationToken cancellationToken = default) { Events.Add(access); return ValueTask.CompletedTask; }
    }
    private sealed class FailingObserver : ISecretAccessObserver
    {
        public ValueTask RecordAsync(SecretAccessEvent access, CancellationToken cancellationToken = default) => throw new IOException("audit unavailable");
    }
    private sealed class Files : IDisposable
    {
        public string Root { get; } = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "qian-secrets-" + Guid.NewGuid().ToString("N"))).FullName;
        public string Outside { get; } = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "qian-secrets-outside-" + Guid.NewGuid().ToString("N"))).FullName;
        public void Write(string name, string value)
        {
            var directory = Directory.CreateDirectory(Path.Combine(Root, "qian", name)).FullName;
            File.WriteAllText(Path.Combine(directory, "token"), value);
        }
        public void Dispose() { Directory.Delete(Root, true); Directory.Delete(Outside, true); }
    }
}
