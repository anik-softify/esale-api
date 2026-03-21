using eSale.Domain.Modules.Tenants.Entities;
using eSale.Domain.Modules.Tenants.Interfaces;
using eSale.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eSale.Infrastructure.Modules.Tenants;

public sealed class TenantRepository : ITenantRepository
{
    private readonly CentralDbContext _context;

    public TenantRepository(CentralDbContext context)
    {
        _context = context;
    }

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Tenant>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .Where(t => t.IsActive)
            .OrderBy(t => t.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        await _context.Tenants.AddAsync(tenant, cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
