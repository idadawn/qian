using Qian.Runtime.Errors;

namespace Qian.Identity.Abstractions.Tests;

public sealed class DirectoryContractTests
{
    [Theory]
    [InlineData(0, 9, true)]
    [InlineData(8, 1, true)]
    [InlineData(1, 9, false)]
    [InlineData(9, 1, false)]
    [InlineData(-1, 1, false)]
    public void 整棵子树深度校验(int parent, int height, bool allowed)
    {
        if (allowed) DirectoryRules.ValidateDepth(parent, height);
        else Assert.Equal("DIRECTORY_DEPTH_EXCEEDED", Assert.Throws<QianErrorException>(() => DirectoryRules.ValidateDepth(parent, height)).Error.Code.Code);
    }
    [Fact]
    public void 状态机及操作安全元数据明确_物理清除无执行入口()
    {
        Assert.True(DirectoryRules.CanTransition(AccountStatuses.Active, AccountStatuses.Disabled));
        Assert.True(DirectoryRules.CanTransition(AccountStatuses.Disabled, AccountStatuses.Active));
        Assert.True(DirectoryRules.CanTransition(AccountStatuses.Disabled, AccountStatuses.Locked));
        Assert.False(DirectoryRules.CanTransition(AccountStatuses.Active, AccountStatuses.Locked));
        Assert.False(DirectoryRules.CanTransition(AccountStatuses.Locked, AccountStatuses.Active));
        Assert.False(DirectoryRules.CanTransition(AccountStatuses.Purged, AccountStatuses.Purged));
        var purge = Assert.Single(DirectoryOperations.All, row => row.Name == "directory.member.purge");
        Assert.Equal(("destructive", false, false), (purge.SideEffect, purge.AgentExpose, purge.Executable));
        Assert.All(DirectoryOperations.All, row => { Assert.NotEmpty(row.Description); Assert.NotEmpty(row.Example); });
    }
}
