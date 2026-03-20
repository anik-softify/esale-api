using AutoMapper;
using eSale.Application.Common.Caching;
using eSale.Application.Common.Interfaces;
using eSale.Application.Modules.Products.Commands;
using eSale.Application.Modules.Products.Mappings;
using eSale.Domain.Common.Interfaces;
using eSale.Domain.Modules.Products.Entities;
using eSale.Domain.Modules.Products.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace eSale.Tests.Modules.Products.Commands;

public sealed class CreateProductCommandHandlerTests
{
    private readonly IMapper _mapper;

    public CreateProductCommandHandlerTests()
    {
        var configuration = new MapperConfiguration(
            cfg => cfg.AddProfile<ProductProfile>(),
            NullLoggerFactory.Instance);
        _mapper = configuration.CreateMapper();
    }

    [Fact]
    public async Task Handle_Should_Create_Product_And_Return_Id()
    {
        var repositoryMock = new Mock<IProductRepository>();
        var tenantProviderMock = new Mock<ITenantProvider>();
        var cacheServiceMock = new Mock<ICacheService>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var tenantId = Guid.NewGuid();
        Product? capturedProduct = null;

        tenantProviderMock.Setup(provider => provider.GetTenantId()).Returns(tenantId);
        repositoryMock
            .Setup(repository => repository.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((product, _) => capturedProduct = product)
            .Returns(Task.CompletedTask);
        cacheServiceMock
            .Setup(cache => cache.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        unitOfWorkMock
            .Setup(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = new CreateProductCommandHandler(
            repositoryMock.Object,
            tenantProviderMock.Object,
            _mapper,
            cacheServiceMock.Object,
            unitOfWorkMock.Object);

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
        unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        cacheServiceMock.Verify(cache => cache.RemoveAsync("products:list", It.IsAny<CancellationToken>()), Times.Once);
    }
}
