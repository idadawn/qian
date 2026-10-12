using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Qian.Data;

/// <summary>安装不可由派生上下文遗漏的 Qian 模型约定。</summary>
public static class QianDataModelExtensions
{
    /// <summary>在业务映射之后应用 Kun 数据约定与 Qian schema 安全检查。</summary>
    public static DbContextOptionsBuilder UseQianDataModel(this DbContextOptionsBuilder options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.ReplaceService<IModelCustomizer, QianModelCustomizer>();
    }
}

internal sealed class QianModelCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);
        if (context is not QianDbContext qian)
            throw new QianDataMisconfiguredException("Qian 模型约定只能用于 QianDbContext。");
        qian.CompleteDataModel(modelBuilder);
    }
}
