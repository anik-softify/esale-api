using AutoMapper;
using eSale.Application.Common.Interfaces;
using eSale.Application.Common.Mappings;
using eSale.Application.Modules.Products.Commands;
using eSale.Application.Common.Caching;
using eSale.Domain.Modules.Products.Entities;
using eSale.Domain.Modules.Products.Interfaces;
using Moq;
using Xunit;

namespace eSale.Tests.Modules.Products.Commands;

public sealed class CreateProductCommandHandlerTests
{
    private readonly IMapper _mapper;

    public CreateProductCommandHandlerTests()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<ProductProfile>());
        _mapper = configuration.CreateMapper();
    }

    [Fact]
    public async Task Handle_Should_Create_Product_And_Return_Id()
    {
        var repositoryMock = new Mock<IProductRepository>();
        var tenantProviderMock = new Mock<ITenantProvider>();
        var cacheServiceMock = new Mock<ICacheService>();
        var tenantId = Guid.NewGuid();
        Product? capturedProduct = null;

        tenantProviderMock.Setup(provider => provider.GetTenantId()).Returns(tenantId);
        repositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((product, _) => capturedProduct = product)
            .Returns(Task.CompletedTask);
        repositoryMock
            .Setup(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        cacheServiceMock
            .Setup(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CreateProductCommandHandler(
            repositoryMock.Object,
            tenantProviderMock.Object,
            _mapper,
            cacheServiceMock.Object);

        var command = new CreateProductCommand(
            "Gaming Laptop",
            "16GB RAM",
            "LAP-100",
            1499.99m,
            5);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result);
        Assert.NotNull(capturedProduct);
        Assert.Equal(result, capturedProduct!.Id);
        Assert.Equal(tenantId, capturedProduct.TenantId);
        Assert.Equal(command.Name, capturedProduct.Name);
        Assert.Equal(command.Description, capturedProduct.Description);
        Assert.Equal(command.Sku, capturedProduct.Sku);
        Assert.Equal(command.Price, capturedProduct.Price);
        Assert.Equal(command.StockQuantity, capturedProduct.StockQuantity);

        repositoryMock.Verify(repository => repository.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
        repositoryMock.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        cacheServiceMock.Verify(cache => cache.RemoveAsync("products:list", It.IsAny<CancellationToken>()), Times.Once);
    }
}
