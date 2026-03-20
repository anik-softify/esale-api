using AutoMapper;
using eSale.Application.Modules.Products.Commands;
using eSale.Application.Modules.Products.DTOs;
using eSale.Domain.Modules.Products.Entities;

namespace eSale.Application.Modules.Products.Mappings;

public sealed class ProductProfile : Profile
{
    public ProductProfile()
    {
        CreateMap<Product, ProductDto>();
        CreateMap<CreateProductCommand, Product>();
    }
}
