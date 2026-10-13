using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kun.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;

namespace Qian.Registry.Tests;

public sealed partial class PatchPersistenceTests
{
    [Fact]
    public async Task 预装元数据在业务组件挂载和监听启动前可读_候选制品不会混入_文件变化不篡改快照()
    {
        var tenant = new AsyncLocalTenantContext();
        await using var running = await CreateRunningRegistry(tenant, start: false);
        var installed = running.Registry.GetInstalledComponents();
        Assert.Equal(4, installed.Count);
        var privateComponent = Assert.Single(installed, component => component.PluginId == "test.private");
        Assert.DoesNotContain(running.Runtime.DumpConfig(), entry => entry.Plugin == "test.private");
        Assert.Equal("record", privateComponent.Metadata["metadata/permissions.json"][0].GetProperty("resource").GetString());
        var (files, digest, _) = ArtifactFixture();
        var artifact = RegistryArtifactContent.Read(files, digest, new SurfaceValidator(), ArtifactOptions);
        var catalog = new PostgresArtifactCatalog(running.Host.Services.GetRequiredService<IDataService>());
        var submitted = await catalog.SubmitAsync(artifact, "publisher", Ct);
        Assert.Equal("Submitted", submitted.State);
        Assert.DoesNotContain(running.Registry.GetInstalledComponents(), component => component.PluginId == "test.catalog");
        running.Profile.ChangePrivateMetadata();
        Assert.Equal("record", Assert.Single(running.Registry.GetInstalledComponents(), component => component.PluginId == "test.private")
            .Metadata["metadata/permissions.json"][0].GetProperty("resource").GetString());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, JsonElement>)privateComponent.Metadata).Clear());
        Assert.DoesNotContain("record", privateComponent.ToString(), StringComparison.Ordinal);
        await running.Registry.DisposeAsync();
        Assert.Throws<ServiceNotReadyException>(() => running.Registry.GetInstalledComponents());
    }

    [Fact]
    public void 预装绑定拒绝骨架和身份不一致_不会仅凭清单通过就冒充可用Provider()
    {
        var (files, digest, _) = ArtifactFixture();
        var content = RegistryArtifactContent.Read(files, digest, new SurfaceValidator(), ArtifactOptions);
        var validation = new SurfaceValidationResult(content.Component, []);
        var plugin = new DeclarativeSurfacePlugin(validation);
        var definition = new PluginDefinition(new PluginEntry("candidate", "test.catalog", "1.0.0", ScopeKey.Global, 0, Patch("{}")), plugin, plugin.Provides);
        var binding = new RegistryComponentBinding(definition, validation);
        Assert.Equal("test.catalog", binding.Metadata.PluginId);
        Assert.Throws<ArgumentException>(() => new RegistryComponentBinding(definition with { Entry = definition.Entry with { Version = "2.0.0" } }, validation));
        Assert.Throws<ArgumentException>(() => new RegistryComponentBinding(definition with { Entry = definition.Entry with { Plugin = "test.other" } }, validation));
        var manifest = JsonNode.Parse(files[".qian-plugin/plugin.json"].Span)!; manifest["x-skeleton"] = true;
        files[".qian-plugin/plugin.json"] = Encoding.UTF8.GetBytes(manifest.ToJsonString());
        var skeleton = RegistryArtifactContent.Read(files, TestDigest(files), new SurfaceValidator(), ArtifactOptions);
        Assert.Throws<ArgumentException>(() => new RegistryComponentBinding(definition, new SurfaceValidationResult(skeleton.Component, [])));
    }

    [Fact]
    public async Task 仅把组件文件放进工作目录不能绕过预装绑定而挂载到租户()
    {
        var tenant = new AsyncLocalTenantContext();
        await using var running = await CreateRunningRegistry(tenant);
        running.Profile.AddUnbound();
        using (tenant.BeginScope("a"))
        {
            await Repository(running.Host, tenant).PutAsync(TenantA, "unbound", Patch("""{"id":"unbound","plugin":"test.unbound","config":{}}"""), 0, "actor", Ct);
            await AssertCode(PlatformErrors.InputInvalid, () => running.Registry.SynchronizeAsync(TenantA, Ct).AsTask());
        }
        Assert.DoesNotContain(running.Runtime.DumpConfig(), entry => entry.Plugin == "test.unbound");
        Assert.DoesNotContain(running.Registry.GetInstalledComponents(), component => component.PluginId == "test.unbound");
        Assert.Empty(running.Commands(TenantA)); Assert.Equal(["test.shared/hello"], running.Commands(TenantB));
    }
}
