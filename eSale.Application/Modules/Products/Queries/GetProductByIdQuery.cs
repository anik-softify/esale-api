using AutoMapper;
using eSale.Application.Common.Exceptions;
using eSale.Application.Modules.Products.DTOs;
using eSale.Domain.Modules.Products.Interfaces;
using MediatR;

namespace eSale.Application.Modules.Products.Queries;

public sealed record GetProductByIdQuery(Guid Id) : IRequest<ProductDto>;

public sealed class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDto>
{
    private readonly IProductRepository _productRepository;
    private readonly IMapper _mapper;

    public GetProductByIdQueryHandler(IProductRepository productRepository, IMapper mapper)
    {
        _productRepository = productRepository;
        _mapper = mapper;
    }

    public async Task<ProductDto> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);

        if (product is null)
        {
            throw new NotFoundException($"Product with id '{request.Id}' was not found.");
        }

        return _mapper.Map<ProductDto>(product);
    }
}
