using System.Text.Json;
using Json.Schema;
using Qian.Runtime.Errors;

namespace Qian.Audit.Native.Tests;

public sealed class NativeAuditConfigurationTests
{
    [Theory]
    [InlineData("{\"tenantBinding\":\"tenant\",\"tenantRole\":\"tenant_role\",\"functionOwner\":\"owner_role\"}", true)]
    [InlineData("{\"tenantBinding\":\"tenant\",\"tenantRole\":\"tenant_role\",\"functionOwner\":\"owner_role\",\"platformBinding\":null}", true)]
    [InlineData("{\"tenantBinding\":\"tenant\",\"tenantRole\":\"tenant_role\",\"functionOwner\":\"owner_role\",\"platformRole\":\"platform_role\"}", true)]
    [InlineData("{\"tenantBinding\":\"tenant\",\"tenantRole\":\"tenant_role\",\"functionOwner\":\"owner_role\",\"platformRole\":\"platform_role\",\"platformBinding\":\"platform\"}", true)]
    [InlineData("{\"tenantBinding\":\"tenant\",\"tenantRole\":\"tenant_role\",\"functionOwner\":\"owner_role\",\"maxAttempts\":0}", false)]
    [InlineData("{\"tenantBinding\":\"tenant\",\"tenantRole\":\"tenant_role\",\"functionOwner\":\"owner_role\",\"maxAttempts\":11}", false)]
    [InlineData("{\"tenantBinding\":\"tenant\",\"tenantRole\":\"tenant_role\",\"functionOwner\":\"owner_role\",\"platformBinding\":\"platform\"}", false)]
    [InlineData("{\"tenantBinding\":\"tenant\",\"tenantRole\":\"tenant_role\",\"functionOwner\":\"owner_role\",\"platformBinding\":\"platform\",\"platformRole\":null}", false)]
    [InlineData("{\"tenantBinding\":\"tenant\",\"tenantRole\":\"Upper\",\"functionOwner\":\"owner_role\"}", false)]
    [InlineData("{\"tenantBinding\":\"tenant\",\"tenantRole\":\"tenant_role\",\"functionOwner\":\"owner_role\",\"secret\":\"unexpected\"}", false)]
    public void 配置Schema与运行时解析保持一致(string json, bool valid)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(valid, JsonSchema.FromText(NativeAuditConfiguration.Schema).Evaluate(document.RootElement).IsValid);
        if (valid) Assert.NotNull(NativeAuditSettings.Read(document.RootElement));
        else Assert.Throws<QianErrorException>(() => NativeAuditSettings.Read(document.RootElement));
    }
    [Theory]
    [InlineData("same", "same", null)]
    [InlineData("tenant", "owner", "tenant")]
    [InlineData("tenant", "owner", "owner")]
    [InlineData("bad';drop", "owner", null)]
    public void 凭据角色不能复用或注入SQL(string tenant, string writer, string? platform) => Assert.Throws<QianErrorException>(() => new NativeAuditRoles(tenant, writer, platform));
}
