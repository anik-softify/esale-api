using eSale.Domain.Modules.Tenants.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eSale.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.DatabaseName).IsRequired().HasMaxLength(200);
        builder.Property(t => t.ConnectionString).HasMaxLength(500);

        builder.HasIndex(t => t.DatabaseName).IsUnique();
        builder.HasIndex(t => t.Name).IsUnique();
    }
}
