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
    internal DbSet<CustomerReceipt> CustomerReceipts => Set<CustomerReceipt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerReceipt>(entity =>
        {
            entity.ToTable("customer_receipts"); entity.HasKey(row => new { row.TenantId, row.IdempotencyKey });
            entity.Property(row => row.TenantId).HasMaxLength(36);
            entity.Property(row => row.RequestHash).HasMaxLength(64).IsRequired();
            entity.Property(row => row.Name).HasMaxLength(200).IsRequired();
        });
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

internal sealed class CustomerReceipt : IMultiTenant
{
    public string? TenantId { get; set; }
    public Guid IdempotencyKey { get; set; }
    public string RequestHash { get; set; } = "";
    public Guid CustomerId { get; set; }
    public string Name { get; set; } = "";
}
