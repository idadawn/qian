using System.Reflection;
using System.Text.Json;
using Json.Schema;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Scheduler.TestHost;

namespace Qian.Scheduler.Tests;

public sealed class SchedulerContractTests
{
    [Fact]
    public void QianJob源生成到组件元数据_现有构造与目录Schema保持兼容()
    {
        var declaration = Assert.Single(typeof(ProbePlugin).Assembly.GetCustomAttributes<QianCodeMetadataAttribute>());
        var code = ((IComponentCodeMetadataFactory)Activator.CreateInstance(declaration.FactoryType)!).Create();
        var job = Assert.Single(code.Jobs); Assert.Equal("probe", job.Code); Assert.Equal("qian.scheduler.probe/run", job.Capability.Id);
        Assert.Same(Assert.Single(code.Capabilities), job.Capability);
        Assert.Equal("probe", ProbeRequest.QianJobCode);
        var legacy = new ComponentCodeMetadata("qian.old", "Old, Assembly", [], Array.Empty<ServiceKey>(), []);
        Assert.Empty(legacy.Jobs);
        using var schemaStream = typeof(IScheduler).Assembly.GetManifestResourceStream("scheduler/job-definition.schema.json")!;
        using var reader = new StreamReader(schemaStream); var schema = JsonSchema.FromText(reader.ReadToEnd());
        Assert.True(schema.Evaluate(JsonSerializer.SerializeToElement(new { businessKey = "expire", target = job.Capability.Id, parameters = new { }, schedule = new { cron = "0 * * * * ?" } })).IsValid);
        Assert.False(schema.Evaluate(JsonSerializer.SerializeToElement(new { businessKey = "expire", target = "https://example.com/job", parameters = new { }, schedule = new { cron = "0 * * * * ?" } })).IsValid);
        Assert.False(schema.Evaluate(JsonSerializer.SerializeToElement(new { businessKey = "expire", target = job.Capability.Id, parameters = new { }, schedule = new { cron = "0 * * * * ?" }, options = new { timeout = "forever" } })).IsValid);
        Assert.Throws<ArgumentException>(() => new ComponentCodeMetadata("qian.other", "Other, Assembly", [], Array.Empty<string>(), [job.Capability], "[]", "[]", null, [job]));
        Assert.Throws<ArgumentException>(() => new ComponentCodeMetadata(code.ComponentId, code.RuntimeEntry, [], Array.Empty<string>(), code.Capabilities, "[]", "[]", null, [job, job]));
    }
}
