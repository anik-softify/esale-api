using AutoMapper;
using eSale.Application.Common.Interfaces;
using eSale.Domain.Modules.Products.Entities;
using eSale.Domain.Modules.Products.Interfaces;
using MediatR;

namespace eSale.Application.Modules.Products.Commands;

// --- Command (the request) ---
public sealed record CreateProductCommand(
    string Name,
    string? Description,
    string Sku,
    decimal Price,
    int StockQuantity) : IRequest<Guid>;

// --- Handler (the use case) ---
public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly IProductRepository _productRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IMapper _mapper;

    public CreateProductCommandHandler(
        IProductRepository productRepository,
        ITenantProvider tenantProvider,
        IMapper mapper)
    {
        _productRepository = productRepository;
        _tenantProvider = tenantProvider;
        _mapper = mapper;
    }

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var product = _mapper.Map<Product>(request);
        product.Id = Guid.NewGuid();
        product.TenantId = _tenantProvider.GetTenantId();
        product.IsActive = true;

        await _productRepository.AddAsync(product, cancellationToken);
        await _productRepository.SaveChangesAsync(cancellationToken);

        return product.Id;
    }
}
