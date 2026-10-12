using System.Globalization;
using Kun.Core.Domain;
using Kun.Core.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Qian.Data;

/// <summary>Qian 在 Kun 数据约定之上补充的系统字段。</summary>
public static class QianDataConventions
{
    /// <summary>删除主体的属性名；没有 CLR 属性时用同名 shadow property，数据库列为 deleted_by。</summary>
    public const string DeletedByPropertyName = "DeletedBy";
}

/// <summary>将直接删除改为软删除，填充删除主体与时间；必须在 Kun 审计字段拦截器之前运行。</summary>
public sealed class QianSoftDeleteInterceptor(ICurrentUser user, TimeProvider clock) : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stamp(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null) return;
        foreach (var entry in context.ChangeTracker.Entries<ISoftDelete>().ToArray())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                foreach (var property in entry.Properties) property.IsModified = false;
                entry.Property(nameof(ISoftDelete.IsDeleted)).CurrentValue = true;
                entry.Property(nameof(ISoftDelete.IsDeleted)).IsModified = true;
            }
            var isDeleted = entry.Property(nameof(ISoftDelete.IsDeleted));
            if (isDeleted.CurrentValue is true && (entry.State == EntityState.Added || isDeleted.OriginalValue is false))
            {
                entry.Property(nameof(ISoftDelete.DeletedAt)).CurrentValue = clock.GetUtcNow().UtcDateTime;
                var actor = entry.Property(QianDataConventions.DeletedByPropertyName);
                actor.CurrentValue = Actor(actor.Metadata.ClrType);
            }
            else
            {
                Preserve(entry, nameof(ISoftDelete.DeletedAt));
                Preserve(entry, QianDataConventions.DeletedByPropertyName);
            }
        }
    }

    private static void Preserve(EntityEntry entry, string name)
    {
        var property = entry.Property(name);
        property.CurrentValue = entry.State == EntityState.Added ? null : property.OriginalValue;
        if (entry.State != EntityState.Added) property.IsModified = false;
    }

    private object? Actor(Type nullableType)
    {
        var subject = user.IsAuthenticated ? user.UserId : null;
        if (subject is null) return null;
        var type = Nullable.GetUnderlyingType(nullableType) ?? nullableType;
        if (type == typeof(string)) return subject;
        if (type == typeof(Guid) && Guid.TryParse(subject, out var guid)) return guid;
        if (type == typeof(long) && long.TryParse(subject, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)) return number;
        if (type == typeof(int) && int.TryParse(subject, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) return integer;
        throw new QianDataMisconfiguredException("当前主体不能转换为 deleted_by 的用户键类型。");
    }
}
