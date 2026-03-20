using eSale.Domain.Modules.Products.Entities;
using eSale.Domain.Modules.Products.Interfaces;
using eSale.Infrastructure.Persistence;
using eSale.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace eSale.Infrastructure.Modules.Products;

public class ProductRepository : GenericRepository<Product>, IProductRepository
{
    public ProductRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        return await DbContext.Products
            .FirstOrDefaultAsync(p => p.Sku == sku, cancellationToken);
    }
}
