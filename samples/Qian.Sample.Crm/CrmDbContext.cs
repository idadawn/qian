using Kun.Core.Domain;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Data;

namespace Qian.Sample.Crm;

public sealed class Customer : AggregateRoot<Guid, string>, IMultiTenant
{
    public Customer() { }
    public Customer(Guid id, string name) { Id = id; Name = name; }
    public string? TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options, ITenantContext tenant) : QianDbContext(options, tenant)
{
    public const string Schema = "plugin_qian_sample_crm";
    public const string ListIndex = "ix_customers_tenant_id_id";
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.Property(customer => customer.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(customer => new { customer.TenantId, customer.Id }).HasDatabaseName(ListIndex);
        });
    }
}

public sealed record CustomerItem(Guid Id, string TenantId, string Name);
public sealed record CustomerPage(int Total, IReadOnlyList<CustomerItem> Items);
