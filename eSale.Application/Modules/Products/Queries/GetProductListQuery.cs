using AutoMapper;
using eSale.Application.Common.Caching;
using eSale.Application.Modules.Products.DTOs;
using eSale.Domain.Modules.Products.Interfaces;
using MediatR;

namespace eSale.Application.Modules.Products.Queries;

// --- Query (the request) ---
public sealed record GetProductListQuery : IRequest<IReadOnlyList<ProductDto>>, ICacheableQuery
{
    public string CacheKey => "products:list";
    public TimeSpan Expiration => TimeSpan.FromMinutes(2);
}

// --- Handler (the use case) ---
public sealed class GetProductListQueryHandler : IRequestHandler<GetProductListQuery, IReadOnlyList<ProductDto>>
{
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;

    public GetProductListQueryHandler(IProductRepository productRepository, IMapper mapper)
    {
        _productRepository = productRepository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ProductDto>> Handle(GetProductListQuery request, CancellationToken cancellationToken)
    {
        var products = await _productRepository.GetAllAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<ProductDto>>(products);
    }
}
