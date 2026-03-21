using eSale.Domain.Modules.Tenants.Entities;
using Microsoft.EntityFrameworkCore;

namespace eSale.Infrastructure.Persistence;

/// <summary>
/// Central database: Tenant registry + Hangfire tables only.
/// Identity has moved to tenant-scoped AppDbContext.
/// </summary>
public class CentralDbContext : DbContext
{
    public CentralDbContext(DbContextOptions<CentralDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new Configurations.TenantConfiguration());
    }
}
