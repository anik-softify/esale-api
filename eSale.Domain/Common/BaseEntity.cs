namespace eSale.Domain.Common;

/// <summary>
/// All entities inherit from this. TenantId ensures every row belongs to a tenant.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
