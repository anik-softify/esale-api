using eSale.Domain.Common.Interfaces;
using eSale.Domain.Modules.Products.Entities;

namespace eSale.Domain.Modules.Products.Interfaces;

/// <summary>
/// Specific repository — NOT a generic IRepository{T}.
/// Only exposes operations that Products actually need.
/// </summary>
public interface IProductRepository : IGenericRepository<Product>
{
    Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);
}
