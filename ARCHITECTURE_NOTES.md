# eSale API Architecture Notes

This note keeps track of architecture decisions that matter now and the ones we may add later.

## Added Now

### `IUnitOfWork`

Files:
- [IUnitOfWork.cs](e:\eSale\eSale-api\eSale.Domain\Common\Interfaces\IUnitOfWork.cs)
- [UnitOfWork.cs](e:\eSale\eSale-api\eSale.Infrastructure\Persistence\UnitOfWork.cs)

Current usage:
- [CreateProductCommand.cs](e:\eSale\eSale-api\eSale.Application\Modules\Products\Commands\CreateProductCommand.cs)

Why it helps:
- gives us an explicit commit boundary
- keeps repositories focused on data access
- prepares the project for future multi-entity transactions

Example future use:
- save `Order`
- save `OrderItems`
- update stock
- commit once through one unit of work

## Not Split Yet

### Module-wise service registration

Current state:
- application services are registered in [DependencyInjection.cs](e:\eSale\eSale-api\eSale.Application\DependencyInjection.cs)
- infrastructure services are registered in [ServiceRegistration.cs](e:\eSale\eSale-api\eSale.Infrastructure\ServiceRegistration.cs)

Why we are not splitting more right now:
- only the Product module is actively built
- more split right now would add structure without much payoff

When it will make sense:
- after `Orders`
- after `Categories`
- after `Inventory`
- after `Notifications`

Possible future shape:
- `AddProductModule()`
- `AddOrderModule()`
- `AddCachingModule()`
- `AddBackgroundJobsModule()`

## Later Improvements

### Domain events / integration events

Current state:
- handlers still do direct side effects like cache invalidation

Benefit later:
- product created -> clear cache
- order placed -> send email
- payment completed -> create accounting entry

### Transactional application services

Current state:
- MediatR handlers are acting as the use-case boundary

Benefit later:
- useful when one use case becomes too large for a single handler
- helps coordinate multiple repositories and business rules cleanly

### Stronger cache abstraction

Current state:
- cache abstraction already exists

Files:
- [ICacheService.cs](e:\eSale\eSale-api\eSale.Application\Common\Caching\ICacheService.cs)
- [CachingBehavior.cs](e:\eSale\eSale-api\eSale.Application\Common\Behaviors\CachingBehavior.cs)
- [RedisCacheService.cs](e:\eSale\eSale-api\eSale.Infrastructure\Caching\RedisCacheService.cs)

Later ideas:
- typed cache keys
- per-feature invalidation helpers
- cache policy conventions

### Background job abstraction

Current state:
- email background job abstraction already exists

Files:
- [IEmailJobService.cs](e:\eSale\eSale-api\eSale.Application\Common\BackgroundJobs\IEmailJobService.cs)
- [EmailJobService.cs](e:\eSale\eSale-api\eSale.Infrastructure\BackgroundJobs\EmailJobService.cs)

Later ideas:
- generic job scheduling abstraction
- recurring jobs
- better retry/failure handling

### Notification / email service expansion

Current state:
- only basic email job example exists

Later ideas:
- order confirmation email
- password reset email
- low stock alert
- invoice email

## Recommendation

For the current stage, the best order is:

1. finish Product module properly
2. add Categories and Images
3. add Edit/Delete flows
4. then expand architecture for Orders, Payments, and Notifications

That keeps the project simple now and still prepares it for bigger modules later.

## Industrial Practice Notes

### Is `GenericRepository + UnitOfWork` okay?

Yes. This is a valid and common pattern.

In this project, the preferred direction is:

- repository handles data access
- `IUnitOfWork` handles commit
- handler/service orchestrates the use case

That is why `SaveChangesAsync()` was removed from the generic repository and kept in `IUnitOfWork`.

### Better industrial direction for this project

For `eSale-api`, the most practical long-term style is:

1. keep CQRS handlers for use cases
2. keep feature-specific repositories like `IProductRepository`
3. keep `GenericRepository<T>` only as a base implementation
4. keep `IUnitOfWork` as the explicit commit boundary
5. later add domain events for side effects
6. later add specification pattern for filtering/search

### Why this is better

- avoids too much generic abstraction
- keeps business language clear
- makes transaction boundaries explicit
- scales better when `Orders`, `Categories`, and `Inventory` are added

### What many industrial teams do

There are usually three common approaches:

#### 1. `DbContext` directly as Unit of Work

Many teams use EF Core `DbContext` directly and avoid a generic repository entirely.

Why:
- less boilerplate
- EF Core already behaves like repository + unit of work in many cases

#### 2. Feature-specific repositories

Teams often prefer:
- `IProductRepository`
- `IOrderRepository`
- `ICategoryRepository`

instead of exposing too much generic behavior to the application layer.

Why:
- domain language stays clear
- custom queries fit naturally

#### 3. `Repository + UnitOfWork + CQRS`

This is the direction this project is following now.

Why:
- good balance between structure and simplicity
- easy to understand while the system is growing

### What to avoid

- repository and unit of work both doing commit
- too many unnecessary abstractions
- giant service layers with unclear responsibility
- over-engineering before more modules exist

### Best current target for `eSale-api`

For this project, the recommended architecture target is:

- `Feature-specific repositories`
- `GenericRepository<T>` only as shared base code
- `IUnitOfWork` for commits
- `CQRS + MediatR` for use cases
- `CachingBehavior` for cacheable queries
- `Background job abstractions` for async work
- later `Domain events`
- later `Specification pattern`
