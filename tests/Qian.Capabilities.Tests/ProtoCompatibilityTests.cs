using System.Text;
using Qian.Runtime.Errors;

namespace Qian.Capabilities.Tests;

public sealed class ProtoCompatibilityTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static BufProtoCompatibilityProbe Probe() => new(Environment.GetEnvironmentVariable("QIAN_BUF_PATH")
        ?? throw new InvalidOperationException("必须预装固定版本 Buf，不能跳过 proto 检测。"));
    [Fact]
    public async Task 真实proto编译保留依赖_兼容新增字段通过_改字段类型拒绝()
    {
        var baseline = Source("string name = 1;");
        Assert.NotEmpty((await Probe().CompileAsync(baseline, Ct)).ToArray());
        Assert.True((await Probe().CompareAsync(baseline, Source("string name = 1; string label = 2;"), Ct)).IsCompatible);
        var breaking = await Probe().CompareAsync(baseline, Source("int32 name = 1;"), Ct);
        Assert.False(breaking.IsCompatible); Assert.NotEmpty(breaking.BreakingRules);
    }
    [Fact]
    public async Task 缺失导入和语法错误不得当作兼容_源路径不能越界()
    {
        var probe = Probe();
        await Assert.ThrowsAsync<QianErrorException>(() => probe.CompileAsync(Source("invalid syntax"), Ct).AsTask());
        var missing = new ProtoContractSet(new Dictionary<string, ReadOnlyMemory<byte>>
            { ["service.proto"] = Encoding.UTF8.GetBytes("syntax = \"proto3\"; import \"missing.proto\"; package sample.v1;") });
        await Assert.ThrowsAsync<QianErrorException>(() => probe.CompileAsync(missing, Ct).AsTask());
        Assert.Throws<ArgumentException>(() => new ProtoContractSet(new Dictionary<string, ReadOnlyMemory<byte>> { ["../escape.proto"] = Encoding.UTF8.GetBytes("bad") }));
    }
    private static ProtoContractSet Source(string fields) => new(new Dictionary<string, ReadOnlyMemory<byte>>
    {
        ["model.proto"] = Encoding.UTF8.GetBytes("syntax = \"proto3\"; package sample.v1; message Customer { " + fields + " }"),
        ["service.proto"] = Encoding.UTF8.GetBytes("syntax = \"proto3\"; package sample.v1; import \"model.proto\"; service Customers { rpc Get(Customer) returns (Customer); }")
    });
}
