using System.Text.Json;
using Kun.MultiTenancy;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Registry.Tests;

public sealed partial class PatchPersistenceTests
{
    [Fact]
    public async Task scope条目Apply失败的异常带条目_组件版本_patch修订与原因码_不带底层异常文本_修复后恢复()
    {
        var tenant = new AsyncLocalTenantContext();
        await using var running = await CreateRunningRegistry(tenant, definition => definition.Entry.Id == "private" ? definition with { Plugin = new FailingApply(definition.Plugin) } : definition);
        using var current = tenant.BeginScope("a");
        var repository = Repository(running.Host, tenant);

        await repository.PutAsync(TenantA, "private", Patch("""{"id":"private","plugin":"test.private","config":{"label":"precondition"}}"""), 0, "operator-a", Ct);
        var refused = await Assert.ThrowsAsync<RegistryScopeApplyException>(() => running.Registry.SynchronizeAsync(TenantA, Ct).AsTask());
        Assert.Equal((TenantA, "private", "test.private@1.0.0", (int?)1, "PRECONDITION_FAILED"), (refused.Scope, refused.EntryId, refused.Plugin, refused.Revision, refused.ReasonCode));
        Assert.Equal("PRECONDITION_FAILED", Assert.IsType<QianErrorException>(refused.InnerException).Error.Code.Code);
        Assert.Equal(["entryId", "plugin", "reasonCode", "revision", "scope"], refused.Data.Keys.Cast<string>().Order(StringComparer.Ordinal));
        Assert.Equal("tenant:a", refused.Data["scope"]); Assert.Equal(1, refused.Data["revision"]); Assert.Equal("PRECONDITION_FAILED", refused.Data["reasonCode"]);
        foreach (var part in new[] { "tenant:a", "private", "test.private@1.0.0", "修订 1", "PRECONDITION_FAILED" }) Assert.Contains(part, refused.Message, StringComparison.Ordinal);
        Assert.Empty(running.Commands(TenantA));

        // 非平台错误码的失败按 INTERNAL 报告；底层异常留在 InnerException，不进入消息与诊断数据。
        await repository.PutAsync(TenantA, "private", Patch("""{"id":"private","plugin":"test.private","config":{"label":"crash"}}"""), 1, "operator-a", Ct);
        var crashed = await Assert.ThrowsAsync<RegistryScopeApplyException>(() => running.Registry.SynchronizeAsync(TenantA, Ct).AsTask());
        Assert.Equal(((int?)2, "INTERNAL"), (crashed.Revision, crashed.ReasonCode));
        Assert.IsType<InvalidOperationException>(crashed.InnerException);
        Assert.DoesNotContain(FailingApply.Secret, crashed.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(crashed.Data.Values.Cast<object?>(), value => value?.ToString()?.Contains(FailingApply.Secret, StringComparison.Ordinal) == true);
        Assert.Empty(running.Commands(TenantA));

        // 自带合法原因码的失败（能力契约在登记阶段被拒绝）沿用其原因码；伪造的原因码不合格式时仍按 INTERNAL。
        await repository.PutAsync(TenantA, "private", Patch("""{"id":"private","plugin":"test.private","config":{"label":"contract"}}"""), 2, "operator-a", Ct);
        var contract = await Assert.ThrowsAsync<RegistryScopeApplyException>(() => running.Registry.SynchronizeAsync(TenantA, Ct).AsTask());
        Assert.Equal(((int?)3, "INPUT_INVALID"), (contract.Revision, contract.ReasonCode));
        Assert.Equal("test.private/probe#/inputSchema/properties/tenantId", Assert.IsType<CapabilityContractException>(contract.InnerException).Location);
        await repository.PutAsync(TenantA, "private", Patch("""{"id":"private","plugin":"test.private","config":{"label":"forged"}}"""), 3, "operator-a", Ct);
        var forged = await Assert.ThrowsAsync<RegistryScopeApplyException>(() => running.Registry.SynchronizeAsync(TenantA, Ct).AsTask());
        Assert.Equal(((int?)4, "INTERNAL"), (forged.Revision, forged.ReasonCode));
        Assert.Empty(running.Commands(TenantA));

        await repository.PutAsync(TenantA, "private", Patch("""{"id":"private","plugin":"test.private","config":{"label":"fixed"}}"""), 4, "operator-a", Ct);
        await running.Registry.SynchronizeAsync(TenantA, Ct);
        Assert.Contains("test.private/hello", running.Commands(TenantA));
    }

    // 按配置决定 Apply 失败方式的包装；其余情况交给真实声明式表面插件。
    private sealed class FailingApply(IQianPlugin inner) : IQianPlugin
    {
        internal const string Secret = "内部细节：连接串与配置原文";
        public IReadOnlyList<ServiceKey> Inject => inner.Inject;
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) =>
            (config.TryGetProperty("label", out var label) ? label.GetString() : null) switch
            {
                "precondition" => throw new QianErrorException(new QianError(PlatformErrors.PreconditionFailed)),
                "crash" => throw new InvalidOperationException(Secret),
                "contract" => throw new CapabilityContractException("test.private", "probe", "test.private/probe#/inputSchema/properties/tenantId", "tenant-field"),
                "forged" => throw new InvalidOperationException(Secret) { Data = { ["reasonCode"] = "drop table; --" } },
                _ => inner.ApplyAsync(ctx, config, ct)
            };
    }
}
