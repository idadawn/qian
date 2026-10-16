using Qian.Runtime.Errors;

namespace Qian.Tenancy.Tests;

public sealed class TenantStateMachineTests
{
    [Theory]
    [InlineData(TenantStatus.Pending)] [InlineData(TenantStatus.Trial)] [InlineData(TenantStatus.Active)] [InlineData(TenantStatus.Grace)]
    public void 冻结可从每个S1状态进入_恢复回原状态(TenantStatus state)
    {
        Assert.Equal(TenantStatus.Suspended, TenantStateMachine.Next(state, "suspend", null, null));
        Assert.Equal(state, TenantStateMachine.Next(TenantStatus.Suspended, "resume", state, "suspend"));
        Assert.Equal(state, TenantStateMachine.Next(state, "resume", state, "resume"));
    }
    [Fact]
    public void 未支持的注销态和非法操作返回结构化状态冲突()
    {
        Assert.Equal("TENANCY_STATE_CONFLICT", Assert.Throws<QianErrorException>(() => TenantStateMachine.Next((TenantStatus)99, "activate", null, null)).Error.Code.Code);
        Assert.Equal("TENANCY_STATE_CONFLICT", Assert.Throws<QianErrorException>(() => TenantStateMachine.Next(TenantStatus.Grace, "activate", null, null)).Error.Code.Code);
        Assert.Equal("TENANCY_STATE_CONFLICT", Assert.Throws<QianErrorException>(() => TenantStateMachine.Next(TenantStatus.Pending, "resume", null, null)).Error.Code.Code);
        Assert.Equal("TENANCY_STATE_CONFLICT", Assert.Throws<QianErrorException>(() => TenantStateMachine.Next(TenantStatus.Active, "purge", null, null)).Error.Code.Code);
    }
    [Fact]
    public void 冻结清除标destructive与humanOnly_扫描不对模型暴露()
    {
        var actions = TenantStateMetadata.Document.GetProperty("actions").EnumerateArray().ToArray();
        foreach (var name in new[] { "suspend", "purge" })
        {
            var action = actions.Single(action => action.GetProperty("name").GetString() == name);
            Assert.Equal("destructive", action.GetProperty("sideEffect").GetString()); Assert.False(action.GetProperty("agent").GetProperty("expose").GetBoolean());
        }
        Assert.False(actions.Single(action => action.GetProperty("name").GetString() == "scan-expired").GetProperty("agent").GetProperty("expose").GetBoolean());
    }
}
