# Add Product Request Flow

This file explains how an `Add Product` request travels through the `eSale-api` backend.

## Request Entry Point

Client sends:

```http
POST /api/products
```

This request first enters the `eSale.Api` project.

Main file:

- [ProductsController.cs](e:\eSale\eSale-api\eSale.Api\Modules\Products\ProductsController.cs#L34)

## Full Request Path

### 1. HTTP Request enters API

The request reaches the ASP.NET Core app through the pipeline configured in:

- [Program.cs](e:\eSale\eSale-api\eSale.Api\Program.cs#L48)

### 2. Middleware Pipeline runs

The request goes through middleware in this order:

1. `GlobalExceptionMiddleware`
2. `TenantMiddleware`
3. Controller routing

Files:

- [GlobalExceptionMiddleware.cs](e:\eSale\eSale-api\eSale.Api\Middleware\GlobalExceptionMiddleware.cs#L19)
- [TenantMiddleware.cs](e:\eSale\eSale-api\eSale.Api\Middleware\TenantMiddleware.cs#L18)

What happens here:

- if any exception happens, `GlobalExceptionMiddleware` catches it
- `TenantMiddleware` checks the tenant header
- if tenant is valid, request moves to controller

### 3. Controller receives the request

Controller file:

- [ProductsController.cs](e:\eSale\eSale-api\eSale.Api\Modules\Products\ProductsController.cs#L34)

The `Create(...)` action executes and sends the request to MediatR:

```csharp
var id = await _mediator.Send(command, cancellationToken);
```

Important:

- controller does not directly talk to the database
- it sends `CreateProductCommand` to the application layer

### 4. MediatR pipeline starts

Before the handler executes, pipeline behaviors run.

Relevant behavior:

- `ValidationBehavior`

File:

- [ValidationBehavior.cs](e:\eSale\eSale-api\eSale.Application\Common\Behaviors\ValidationBehavior.cs#L16)

### 5. FluentValidation runs

Validator file:

- [CreateProductCommandValidator.cs](e:\eSale\eSale-api\eSale.Application\Modules\Products\Commands\CreateProductCommandValidator.cs#L7)

Validation checks things like:

- product name exists
- SKU exists
- price is valid
- stock quantity is valid

If validation fails:

- `ValidationException` is thrown
- response goes back to `GlobalExceptionMiddleware`
- API returns JSON error response

### 6. Command handler executes

File:

- [CreateProductCommand.cs](e:\eSale\eSale-api\eSale.Application\Modules\Products\Commands\CreateProductCommand.cs#L38)

This file contains:

- `CreateProductCommand`
- `CreateProductCommandHandler`

Inside the handler:

1. request maps to `Product` entity
2. `Id = Guid.NewGuid()`
3. tenant id comes from `ITenantProvider`
4. `IsActive = true`
5. repository saves the product
6. cache invalidation happens

### 7. Tenant provider is used

Interface:

- [ITenantProvider.cs](e:\eSale\eSale-api\eSale.Application\Common\Interfaces\ITenantProvider.cs)

Implementation:

- [TenantProvider.cs](e:\eSale\eSale-api\eSale.Api\Middleware\TenantProvider.cs#L18)

Meaning:

- tenant info is captured in middleware
- handler reads it through the provider

### 8. Repository layer is called

Handler depends on:

- `IProductRepository`

Interface:

- [IProductRepository.cs](e:\eSale\eSale-api\eSale.Domain\Modules\Products\Interfaces\IProductRepository.cs#L10)

Implementation:

- [ProductRepository.cs](e:\eSale\eSale-api\eSale.Infrastructure\Modules\Products\ProductRepository.cs#L9)

Generic repository pieces:

- [IGenericRepository.cs](e:\eSale\eSale-api\eSale.Domain\Common\Interfaces\IGenericRepository.cs#L5)
- [GenericRepository.cs](e:\eSale\eSale-api\eSale.Infrastructure\Persistence\Repositories\GenericRepository.cs#L31)

### 9. DbContext is hit

Repository eventually uses:

- [AppDbContext.cs](e:\eSale\eSale-api\eSale.Infrastructure\Persistence\AppDbContext.cs#L32)

Here EF Core:

- tracks the entity
- prepares SQL
- saves data into MySQL

### 10. Domain entity is used

Entity:

- [Product.cs](e:\eSale\eSale-api\eSale.Domain\Modules\Products\Entities\Product.cs#L5)

Base entity:

- [BaseEntity.cs](e:\eSale\eSale-api\eSale.Domain\Common\BaseEntity.cs#L6)

### 11. Table configuration is applied

Entity configuration file:

- [ProductConfiguration.cs](e:\eSale\eSale-api\eSale.Infrastructure\Persistence\Configurations\ProductConfiguration.cs#L9)

This helps EF Core map the entity correctly to the `Products` table.

### 12. Response returns back

After save:

- handler returns `Guid`
- controller returns `201 Created`

Return flow:

`DbContext -> Repository -> Handler -> MediatR -> Controller -> HTTP Response`

## Short One-Line Flow

```text
HTTP Request
-> Program.cs middleware
-> GlobalExceptionMiddleware
-> TenantMiddleware
-> ProductsController
-> MediatR
-> ValidationBehavior
-> CreateProductCommandValidator
-> CreateProductCommandHandler
-> ProductRepository / GenericRepository
-> AppDbContext
-> MySQL
-> back to Handler
-> back to Controller
-> HTTP Response
```

## If Validation Fails

```text
Request
-> Middleware
-> Controller
-> MediatR
-> ValidationBehavior
-> ValidationException
-> GlobalExceptionMiddleware
-> JSON error response
```

## Exact File-by-File Path

1. [Program.cs](e:\eSale\eSale-api\eSale.Api\Program.cs#L48)
2. [GlobalExceptionMiddleware.cs](e:\eSale\eSale-api\eSale.Api\Middleware\GlobalExceptionMiddleware.cs#L19)
3. [TenantMiddleware.cs](e:\eSale\eSale-api\eSale.Api\Middleware\TenantMiddleware.cs#L18)
4. [ProductsController.cs](e:\eSale\eSale-api\eSale.Api\Modules\Products\ProductsController.cs#L34)
5. [ValidationBehavior.cs](e:\eSale\eSale-api\eSale.Application\Common\Behaviors\ValidationBehavior.cs#L16)
6. [CreateProductCommandValidator.cs](e:\eSale\eSale-api\eSale.Application\Modules\Products\Commands\CreateProductCommandValidator.cs#L7)
7. [CreateProductCommand.cs](e:\eSale\eSale-api\eSale.Application\Modules\Products\Commands\CreateProductCommand.cs#L38)
8. [ITenantProvider.cs](e:\eSale\eSale-api\eSale.Application\Common\Interfaces\ITenantProvider.cs)
9. [TenantProvider.cs](e:\eSale\eSale-api\eSale.Api\Middleware\TenantProvider.cs#L18)
10. [IProductRepository.cs](e:\eSale\eSale-api\eSale.Domain\Modules\Products\Interfaces\IProductRepository.cs#L10)
11. [ProductRepository.cs](e:\eSale\eSale-api\eSale.Infrastructure\Modules\Products\ProductRepository.cs#L9)
12. [IGenericRepository.cs](e:\eSale\eSale-api\eSale.Domain\Common\Interfaces\IGenericRepository.cs#L5)
13. [GenericRepository.cs](e:\eSale\eSale-api\eSale.Infrastructure\Persistence\Repositories\GenericRepository.cs#L31)
14. [AppDbContext.cs](e:\eSale\eSale-api\eSale.Infrastructure\Persistence\AppDbContext.cs#L32)
15. [Product.cs](e:\eSale\eSale-api\eSale.Domain\Modules\Products\Entities\Product.cs#L5)
16. [BaseEntity.cs](e:\eSale\eSale-api\eSale.Domain\Common\BaseEntity.cs#L6)
17. [ProductConfiguration.cs](e:\eSale\eSale-api\eSale.Infrastructure\Persistence\Configurations\ProductConfiguration.cs#L9)
