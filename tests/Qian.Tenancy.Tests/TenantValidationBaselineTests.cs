using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;
using Qian.Runtime.Errors;

namespace Qian.Tenancy.Tests;

public sealed partial class TenantDirectoryTests
{
    // DEC-025：此用例先在未修改的 159bedf99a 服务端执行，再与纠偏后实现复验。
    // 不依赖新增 DTO 标注、Schema 或字段 pointer，避免以新行为证明旧行为。
    [Theory]
    [InlineData("minLength")]
    [InlineData("maxLength")]
    [InlineData("pattern")]
    public async Task 历史名称约束_创建和编辑均拒绝且不改变数据库(string constraint)
    {
        using var host = Host(); await host.StartAsync(Ct); await Prepare(host); var store = Store(host);
        var valid = await store.CreateAsync(new("baseline", "原名称"), Ct);
        var names = constraint switch
        {
            "minLength" => new[] { "" },
            "maxLength" => new[] { new string('名', 201), string.Concat(Enumerable.Repeat("😀", 101)) },
            _ => new[] { " ", "\u00a0", "\u3000", "bad\nname", "bad\u007fname", "bad\u009fname" }
        };
        foreach (var name in names)
        {
            var create = await Assert.ThrowsAsync<QianErrorException>(() => store.CreateAsync(new("rejected", name), Ct));
            Assert.Equal("INPUT_INVALID", create.Error.Code.Code);
            var update = await Assert.ThrowsAsync<QianErrorException>(() => store.UpdateAsync(valid.Id, new(name, valid.Revision), Ct));
            Assert.Equal("INPUT_INVALID", update.Error.Code.Code);
            Assert.Null(await store.GetAsync("rejected", Ct));
            Assert.Equal(valid, await store.GetByIdAsync(valid.Id, Ct));
        }
        await using var session = await host.Services.GetRequiredService<IDataService>().OpenAsync<TenancyDbContext>(Ct);
        Assert.Single(await session.Context.Tenants.ToArrayAsync(Ct));
    }
}
