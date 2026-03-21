# Project Request Cycle

Deep documentation of how every HTTP request flows through eSale-api, which line receives it, how validation works, and how data reaches the database.

---

## Architecture Layers

```
eSale.Api           → HTTP entry, middleware, controllers
eSale.Application   → Commands, queries, validators, behaviors, DTOs
eSale.Domain        → Entities, repository interfaces, base types
eSale.Infrastructure→ EF Core, repositories, caching, jobs, DB contexts
```

Dependency rule: each layer only depends on the layer directly inside it.

```
Api → Application → Domain ← Infrastructure
```

---

## 1. Application Startup (Program.cs)

File: `eSale.Api/Program.cs`

### Service Registration (lines 35-62)

```
Line 35: builder.Services.AddApplication()
```
Registers MediatR, AutoMapper, FluentValidation, and pipeline behaviors (ValidationBehavior, CachingBehavior).

Source: `eSale.Application/DependencyInjection.cs` lines 12-17:
- Line 12: MediatR scans the assembly for all handlers
- Line 14: AutoMapper scans for all Profile classes
- Line 15: FluentValidation scans for all AbstractValidator<T> classes
- Line 16: ValidationBehavior registered as pipeline behavior (runs before every handler)
- Line 17: CachingBehavior registered as pipeline behavior (runs for ICacheableQuery requests)

```
Line 36: builder.Services.AddInfrastructure(builder.Configuration)
```
Registers DbContexts, Identity, repositories, Redis, Hangfire.

Source: `eSale.Infrastructure/DependencyInjection.cs`:
- Lines 39-40: CentralDbContext (static connection → esale_central)
- Lines 43-57: AppDbContext (scoped factory → tenant-specific connection)
- Lines 60-70: ASP.NET Identity uses AppDbContext (tenant-scoped)
- Lines 87-104: Hangfire uses central connection
- Lines 107-114: Repository and service registrations

### Middleware Pipeline (lines 79-86)

```
Line 79: app.UseSerilogRequestLogging()     → structured logging
Line 80: app.UseMiddleware<GlobalExceptionMiddleware>()  → catches all exceptions
Line 81: app.UseHttpsRedirection()
Line 82: app.UseAuthentication()             → JWT token validation, populates User claims
Line 83: app.UseMiddleware<TenantMiddleware>()→ tenant resolution (after auth so JWT claims are available)
Line 84: app.UseAuthorization()
Line 85: app.UseHangfireDashboard("/hangfire")
Line 86: app.MapControllers()                → route matching
```

**Order matters**: Authentication runs before TenantMiddleware so that JWT `tenantId` claims are available for cross-validation.

---

## 2. Middleware Deep Dive

### GlobalExceptionMiddleware

File: `eSale.Api/Middleware/GlobalExceptionMiddleware.cs`

**What it does**: Wraps the entire pipeline in a try-catch. Any unhandled exception is caught and converted to a structured JSON response.

- Line 19: `InvokeAsync` — calls `_next(context)` inside try block
- Line 27: On exception, logs with trace ID
- Lines 34-71: Pattern matches exception type to HTTP status:
  - `ValidationException` → 400 with grouped field errors
  - `NotFoundException` → 404
  - `UnauthorizedAccessException` → 401
  - `BadHttpRequestException` → 400
  - Everything else → 500

Response format (from `eSale.Api/Common/ApiExceptionResponse.cs`):
```json
{
  "statusCode": 400,
  "message": "One or more validation failures occurred.",
  "errors": { "Name": ["'Name' must not be empty."] },
  "traceId": "00-abc123..."
}
```

### TenantMiddleware

File: `eSale.Api/Middleware/TenantMiddleware.cs`

**What it does**: Resolves which tenant database this request targets.

- Lines 15-18: Excluded paths — `/api/tenants` and `/hangfire` skip tenant resolution
- Line 35: `TryResolveTenantId` — tries to find tenant ID from two sources:
  - Lines 57-61: **X-Tenant-Id header** (for unauthenticated requests like register/login)
  - Lines 63-68: **JWT `tenantId` claim** (for authenticated requests)
  - Lines 70-73: If both are present and don't match → throws `UnauthorizedAccessException`
- Lines 43-45: Resolves connection string via `ITenantConnectionResolver`
- Lines 40, 46: Stores `TenantId` and `TenantConnectionString` in `HttpContext.Items`

### TenantProvider

File: `eSale.Api/Middleware/TenantProvider.cs`

**What it does**: Reads tenant data set by TenantMiddleware from HttpContext.Items.

- Line 25: `GetTenantId()` — reads `HttpContext.Items["TenantId"]`
- Line 38: `GetConnectionString()` — reads `HttpContext.Items["TenantConnectionString"]`
- Lines 23, 36: If no HTTP context (background job) → throws `InvalidOperationException`
- Lines 28, 41: If items not found → throws `InvalidOperationException`

**No silent fallbacks** — if tenant context is missing, the app fails loudly.

---

## 3. Full Request Flow: Create Product

### HTTP Request

```http
POST /api/products
X-Tenant-Id: a1b2c3d4-...
Content-Type: application/json

{
  "name": "Widget",
  "description": "A fine widget",
  "sku": "WDG-001",
  "price": 29.99,
  "stockQuantity": 100
}
```

### Step-by-step execution

#### Step 1: GlobalExceptionMiddleware (line 19)
Wraps everything. If anything below throws, it catches and returns JSON.

#### Step 2: Authentication middleware
ASP.NET reads the `Authorization: Bearer <token>` header, validates the JWT signature, and populates `HttpContext.User` with claims (sub, email, tenantId, etc.).

For this request (no auth header needed for product create if not configured), the user may or may not be authenticated.

#### Step 3: TenantMiddleware (line 26)

1. Path is `/api/products` — not excluded
2. `TryResolveTenantId` checks header → finds `X-Tenant-Id` → parses GUID
3. `ITenantConnectionResolver.GetConnectionStringAsync` is called
   - Source: `eSale.Infrastructure/Persistence/TenantConnectionResolver.cs`
   - Line 20: Looks up tenant in central DB via `ITenantRepository`
   - Line 22: Validates tenant exists and is active
   - Lines 25-26: If tenant has custom connection string, use it
   - Lines 29-32: Otherwise, take DefaultConnection template and replace `Database=` with tenant's `DatabaseName`
4. Stores tenantId + connectionString in `HttpContext.Items`

#### Step 4: Controller routing → ProductsController (line 35)

File: `eSale.Api/Modules/Products/ProductsController.cs`

```csharp
[HttpPost]                              // line 34
public async Task<ActionResult<Guid>> Create(
    [FromBody] CreateProductCommand command,   // line 36 — ASP.NET deserializes JSON → record
    CancellationToken cancellationToken)
{
    var id = await _mediator.Send(command, cancellationToken);  // line 39
    return CreatedAtAction(nameof(GetById), new { id }, id);    // line 40 — returns 201
}
```

- Line 36: ASP.NET model binding deserializes the JSON body into `CreateProductCommand` record
- Line 39: `_mediator.Send()` enters the MediatR pipeline

#### Step 5: MediatR Pipeline — ValidationBehavior (line 16)

File: `eSale.Application/Common/Behaviors/ValidationBehavior.cs`

```csharp
public async Task<TResponse> Handle(TRequest request, ...)     // line 16
{
    if (_validators.Any())                                       // line 21
    {
        var context = new ValidationContext<TRequest>(request);   // line 23
        var results = await Task.WhenAll(                         // line 24
            _validators.Select(v => v.ValidateAsync(context)));
        var failures = results.SelectMany(r => r.Errors)...;    // line 27-30
        if (failures.Count != 0)
            throw new ValidationException(failures);             // line 34
    }
    return await next();                                         // line 38
}
```

How validators are found:
- DI registered all `IValidator<T>` via `AddValidatorsFromAssembly` in `Application/DependencyInjection.cs` line 15
- For `CreateProductCommand`, the matching validator is `CreateProductCommandValidator`

#### Step 6: FluentValidation runs

File: `eSale.Application/Modules/Products/Commands/CreateProductCommandValidator.cs`

```csharp
RuleFor(x => x.Name).NotEmpty().MaximumLength(200);        // line 9-10
RuleFor(x => x.Description).MaximumLength(2000);           // line 13
RuleFor(x => x.Sku).NotEmpty().MaximumLength(50);          // line 16-17
RuleFor(x => x.Price).GreaterThan(0);                      // line 20
RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);     // line 23
```

If validation fails:
```
ValidationBehavior throws ValidationException
  → GlobalExceptionMiddleware catches it (line 36)
  → Returns 400 with grouped errors
```

Example error response:
```json
{
  "statusCode": 400,
  "message": "One or more validation failures occurred.",
  "errors": {
    "Name": ["'Name' must not be empty."],
    "Price": ["'Price' must be greater than '0'."]
  }
}
```

#### Step 7: MediatR Pipeline — CachingBehavior (line 25)

File: `eSale.Application/Common/Behaviors/CachingBehavior.cs`

- Line 30: Checks if request implements `ICacheableQuery` → `CreateProductCommand` does NOT
- Line 33: Skips cache, calls `next()` immediately

(CachingBehavior only applies to queries like `GetProductListQuery`, not commands.)

#### Step 8: CreateProductCommandHandler (line 42)

File: `eSale.Application/Modules/Products/Commands/CreateProductCommand.cs`

```csharp
public async Task<Guid> Handle(CreateProductCommand request, ...)  // line 42
{
    var product = _mapper.Map<Product>(request);     // line 44 — AutoMapper
    product.Id = Guid.NewGuid();                     // line 45
    product.TenantId = _tenantProvider.GetTenantId(); // line 46
    product.IsActive = true;                          // line 47

    await _productRepository.AddAsync(product);       // line 49
    await _unitOfWork.SaveChangesAsync();             // line 50
    await _cacheService.RemoveAsync(...);             // line 51
    return product.Id;                                // line 53
}
```

Key operations:
1. **AutoMapper** maps command → Product entity (profile in `Application/Modules/Products/Mappings/ProductProfile.cs` line 13)
2. **TenantProvider** returns the tenant ID from HttpContext.Items (set by middleware)
3. **Repository** stages the entity (not yet saved)
4. **UnitOfWork** commits to database
5. **Cache invalidation** removes the product list cache for this tenant

#### Step 9: Repository Layer

File: `eSale.Infrastructure/Persistence/Repositories/GenericRepository.cs`

```csharp
public virtual async Task AddAsync(T entity, ...)       // line 31
{
    await DbSet.AddAsync(entity, cancellationToken);    // line 33 — EF tracks entity
}
```

`ProductRepository` (line 9 in `Infrastructure/Modules/Products/ProductRepository.cs`) extends `GenericRepository<Product>`. The `AddAsync` call marks the entity as `Added` in EF's change tracker — no SQL is executed yet.

#### Step 10: UnitOfWork commits

File: `eSale.Infrastructure/Persistence/UnitOfWork.cs`

```csharp
public Task<int> SaveChangesAsync(...)                   // line 14
{
    return _dbContext.SaveChangesAsync(cancellationToken); // line 16
}
```

This calls `AppDbContext.SaveChangesAsync()`.

#### Step 11: AppDbContext.SaveChangesAsync override

File: `eSale.Infrastructure/Persistence/AppDbContext.cs`

```csharp
public override Task<int> SaveChangesAsync(...)          // line 27
{
    foreach (var entry in ChangeTracker.Entries<BaseEntity>())  // line 29
    {
        case EntityState.Added:
            entry.Entity.TenantId = _tenantId;           // line 34 — stamps tenant
            entry.Entity.CreatedAt = DateTime.UtcNow;    // line 35 — stamps timestamp
    }
    return base.SaveChangesAsync();                      // line 42 — EF generates SQL
}
```

EF Core generates:
```sql
INSERT INTO Products (Id, TenantId, Name, Description, Sku, Price, StockQuantity, IsActive, CreatedAt, UpdatedAt)
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9);
```

This SQL runs against the **tenant-specific database** (e.g., `esale_tenant_acme`), because `AppDbContext` was constructed with that tenant's connection string (DI factory in `Infrastructure/DependencyInjection.cs` lines 43-57).

#### Step 12: Response returns

```
AppDbContext.SaveChangesAsync → returns row count
  → UnitOfWork.SaveChangesAsync → returns
    → Handler returns product.Id (Guid)
      → MediatR returns to controller
        → ProductsController returns CreatedAtAction (201)
          → ASP.NET serializes to JSON
```

Response:
```http
HTTP/1.1 201 Created
Location: /api/products/{id}

"a1b2c3d4-..."
```

---

## 4. Full Request Flow: Get Product List (with Caching)

### HTTP Request

```http
GET /api/products
X-Tenant-Id: a1b2c3d4-...
```

### Step-by-step

#### Steps 1-4: Same as above (middleware → controller)

Controller: `ProductsController.cs` line 30:
```csharp
var products = await _mediator.Send(new GetProductListQuery(), cancellationToken);
```

#### Step 5: ValidationBehavior

No validators registered for `GetProductListQuery` → `_validators` is empty → skips to `next()`.

#### Step 6: CachingBehavior kicks in

File: `eSale.Application/Common/Behaviors/CachingBehavior.cs`

```csharp
if (request is not ICacheableQuery cacheableQuery)   // line 30
    return await next();
// GetProductListQuery implements ICacheableQuery!

var tenantId = _tenantProvider.GetTenantId();         // line 35
var scopedKey = $"tenant:{tenantId}:{cacheableQuery.CacheKey}";  // line 36
// Key becomes: "tenant:a1b2c3d4-...:products:list"

var cachedResponse = await _cacheService.GetAsync<TResponse>(scopedKey);  // line 38
if (cachedResponse is not null)
    return cachedResponse;                            // line 42 — CACHE HIT, skip DB

var response = await next();                          // line 46 — CACHE MISS, hit DB
await _cacheService.SetAsync(scopedKey, response, cacheableQuery.Expiration);  // line 47
return response;
```

`GetProductListQuery` (file: `Application/Modules/Products/Queries/GetProductListQuery.cs`):
- Line 12: `CacheKey => "products:list"`
- Line 13: `Expiration => TimeSpan.FromMinutes(2)`

Cache key is tenant-scoped: `tenant:{guid}:products:list` — tenants never see each other's cached data.

#### Step 7: Handler (on cache miss)

File: `eSale.Application/Modules/Products/Queries/GetProductListQuery.cs` line 28:
```csharp
var products = await _productRepository.GetAllAsync(cancellationToken);  // line 30
return _mapper.Map<IReadOnlyList<ProductDto>>(products);                 // line 31
```

Repository calls `GenericRepository.GetAllAsync()` (line 23-28):
```csharp
return await DbSet.AsNoTracking()
    .OrderByDescending(entity => entity.CreatedAt)
    .ToListAsync(cancellationToken);
```

This hits the **tenant database** and returns only that tenant's products.

---

## 5. Full Request Flow: Register User

### HTTP Request

```http
POST /api/account/register
X-Tenant-Id: a1b2c3d4-...
Content-Type: application/json

{
  "firstName": "Anik",
  "lastName": "Das",
  "email": "anik@example.com",
  "password": "MyPassword1"
}
```

### Step-by-step

#### Steps 1-3: Middleware

- TenantMiddleware resolves tenant from `X-Tenant-Id` header
- Connection string resolved → stored in HttpContext.Items
- **Key point**: `/api/account` is NOT excluded — Identity is tenant-scoped

#### Step 4: AccountController (line 22)

File: `eSale.Api/Modules/Auth/AccountController.cs`

```csharp
var result = await _mediator.Send(command, cancellationToken);  // line 24
```

#### Step 5: RegisterCommandHandler (line 38)

File: `eSale.Application/Modules/Auth/Commands/RegisterCommand.cs`

```csharp
var tenantId = _tenantProvider.GetTenantId();           // line 41
var user = _mapper.Map<ApplicationUser>(request);       // line 43
// AutoMapper maps FirstName, LastName, Email → ApplicationUser
// UserName set to Email (AuthMappingProfile.cs line 12)

var result = await _userManager.CreateAsync(user, request.Password);  // line 45
```

`UserManager<ApplicationUser>` uses `AppDbContext` (tenant-scoped) as its store. So `CreateAsync`:
1. Hashes the password
2. Validates email uniqueness **within this tenant's database only**
3. INSERTs into the `AspNetUsers` table in `esale_tenant_acme`

```csharp
var token = _jwtTokenService.GenerateToken(              // line 51
    user.Id, user.Email!, user.FirstName, user.LastName, tenantId);
```

JWT includes `tenantId` claim → future requests can use this for tenant resolution.

---

## 6. Full Request Flow: Login User

### HTTP Request

```http
POST /api/account/login
X-Tenant-Id: a1b2c3d4-...
Content-Type: application/json

{
  "email": "anik@example.com",
  "password": "MyPassword1"
}
```

### Step-by-step

#### LoginCommandHandler (line 32)

File: `eSale.Application/Modules/Auth/Commands/LoginCommand.cs`

```csharp
var tenantId = _tenantProvider.GetTenantId();                           // line 33
var user = await _userManager.FindByEmailAsync(request.Email);          // line 35
// Queries AspNetUsers in the TENANT database (not central)
if (user is null) throw new UnauthorizedAccessException(...);           // line 36

var validPassword = await _userManager.CheckPasswordAsync(user, ...);  // line 38
if (!validPassword) throw new UnauthorizedAccessException(...);         // line 40

var token = _jwtTokenService.GenerateToken(..., tenantId);             // line 42
```

**Tenant isolation**: `FindByEmailAsync` only searches the current tenant's `AspNetUsers` table. Same email in different tenants = different users.

---

## 7. Tenant Provisioning Flow

### HTTP Request

```http
POST /api/tenants/provision
Content-Type: application/json

{"name": "Acme Corp"}
```

Note: This endpoint is **excluded** from TenantMiddleware (no X-Tenant-Id needed).

### ProvisionTenantCommandHandler

File: `eSale.Application/Modules/Tenants/Commands/ProvisionTenantCommand.cs`

```csharp
var tenant = new Tenant
{
    Id = Guid.NewGuid(),                                               // line 26
    DatabaseName = $"esale_tenant_{request.Name.ToLowerInvariant()...}", // line 29
    IsActive = true,
};
await _tenantRepository.AddAsync(tenant);          // line 34 — saves to central DB
await _tenantRepository.SaveChangesAsync();         // line 35

await _tenantDbInitializer.InitializeTenantDatabaseAsync(tenant.Id);  // line 37
```

`TenantDbInitializer.InitializeTenantDatabaseAsync` (file: `Infrastructure/Persistence/TenantDbInitializer.cs`):
1. Line 51: Resolves connection string for the new tenant
2. Lines 64-70: Executes `CREATE DATABASE IF NOT EXISTS` via raw MySQL connection
3. Lines 74-79: Creates a temporary `AppDbContext` and calls `EnsureCreatedAsync()` — this creates all tables (Products, AspNetUsers, AspNetRoles, etc.) in the new tenant database

---

## 8. Database Context Routing

### How AppDbContext gets the right connection

```
HTTP Request
  → TenantMiddleware stores connection string in HttpContext.Items
    → DI factory resolves AppDbContext (Infrastructure/DependencyInjection.cs lines 43-57):
      1. Gets ITenantProvider
      2. Calls tenantProvider.GetConnectionString()
      3. If empty → throws InvalidOperationException (NO fallback)
      4. Builds DbContextOptions with the tenant connection
      5. Returns new AppDbContext(options, tenantProvider)
```

### Two database contexts

| Context | Database | Contains | Registered as |
|---------|----------|----------|---------------|
| CentralDbContext | esale_central | Tenants, Hangfire | `AddDbContext` (singleton connection) |
| AppDbContext | esale_tenant_X | Products, AspNetUsers, AspNetRoles | Scoped factory (dynamic connection) |

---

## 9. Validation Error Flow (detailed)

```
Client sends invalid data (e.g., empty Name, Price = -5)
  → Controller deserializes → CreateProductCommand
    → MediatR sends command
      → ValidationBehavior (line 16)
        → Finds CreateProductCommandValidator via DI
        → Runs all rules in parallel (line 24)
        → Collects failures (line 27-30)
        → Throws ValidationException with all errors (line 34)
      → Exception bubbles up through MediatR
    → Exception bubbles through controller
  → GlobalExceptionMiddleware catches (line 25)
    → Pattern matches ValidationException (line 36)
    → Groups errors by property name (lines 40-44)
    → Returns 400 JSON response
```

---

## 10. One-Line Flow Summary

### Create Product
```
HTTP POST → GlobalExceptionMiddleware → Authentication → TenantMiddleware
  → ProductsController.Create (line 35)
    → MediatR.Send
      → ValidationBehavior → CreateProductCommandValidator
      → CachingBehavior (skips — not a query)
      → CreateProductCommandHandler
        → AutoMapper → TenantProvider → ProductRepository.AddAsync
        → UnitOfWork.SaveChangesAsync → AppDbContext.SaveChangesAsync
          → EF Core → MySQL (tenant database)
        → CacheService.RemoveAsync (invalidate list cache)
    → 201 Created
```

### Get Products (with cache)
```
HTTP GET → ... → TenantMiddleware
  → ProductsController.GetAll (line 28)
    → MediatR.Send
      → ValidationBehavior (no validators — skips)
      → CachingBehavior
        → Cache HIT? → return cached data
        → Cache MISS? → GetProductListQueryHandler
          → ProductRepository.GetAllAsync → AppDbContext → MySQL
          → Cache result for 2 minutes
    → 200 OK
```

### Register
```
HTTP POST → ... → TenantMiddleware (resolves tenant DB)
  → AccountController.Register (line 22)
    → MediatR.Send
      → RegisterCommandHandler
        → UserManager.CreateAsync → AppDbContext (tenant DB) → MySQL
        → JwtTokenService.GenerateToken (includes tenantId claim)
    → 200 OK with JWT
```

### Login
```
HTTP POST → ... → TenantMiddleware (resolves tenant DB)
  → AccountController.Login (line 30)
    → MediatR.Send
      → LoginCommandHandler
        → UserManager.FindByEmailAsync → tenant DB
        → UserManager.CheckPasswordAsync
        → JwtTokenService.GenerateToken
    → 200 OK with JWT
```

### Provision Tenant
```
HTTP POST → GlobalExceptionMiddleware (TenantMiddleware SKIPPED)
  → TenantsController.Provision (line 19)
    → MediatR.Send
      → ProvisionTenantCommandHandler
        → TenantRepository.AddAsync → CentralDbContext → esale_central
        → TenantDbInitializer → CREATE DATABASE → EnsureCreated
    → 200 OK with tenant GUID
```

---

## 11. File Reference Map

### eSale.Api
| File | Purpose |
|------|---------|
| `Program.cs` | App startup, middleware pipeline, DI registration |
| `Middleware/GlobalExceptionMiddleware.cs` | Catches all exceptions → JSON responses |
| `Middleware/TenantMiddleware.cs` | Resolves tenant from header/JWT → connection string |
| `Middleware/TenantProvider.cs` | Reads tenant context from HttpContext.Items |
| `Modules/Products/ProductsController.cs` | Product CRUD endpoints |
| `Modules/Auth/AccountController.cs` | Register + Login endpoints |
| `Modules/Tenants/TenantsController.cs` | Tenant provisioning endpoint |
| `Common/ApiExceptionResponse.cs` | Standardized error response shape |

### eSale.Application
| File | Purpose |
|------|---------|
| `DependencyInjection.cs` | Registers MediatR, AutoMapper, FluentValidation, behaviors |
| `Common/Behaviors/ValidationBehavior.cs` | Runs validators before every handler |
| `Common/Behaviors/CachingBehavior.cs` | Tenant-scoped Redis caching for queries |
| `Common/Caching/ICacheableQuery.cs` | Marker interface for cacheable queries |
| `Common/Caching/ICacheService.cs` | Cache abstraction |
| `Common/Interfaces/ITenantProvider.cs` | Tenant context abstraction |
| `Common/Interfaces/ITenantConnectionResolver.cs` | Tenant → connection string resolution |
| `Common/Interfaces/ITenantDbInitializer.cs` | Tenant DB initialization abstraction |
| `Common/Interfaces/IJwtTokenService.cs` | JWT generation abstraction |
| `Common/Exceptions/NotFoundException.cs` | 404 exception |
| `Modules/Products/Commands/CreateProductCommand.cs` | Create product command + handler |
| `Modules/Products/Commands/CreateProductCommandValidator.cs` | Validation rules |
| `Modules/Products/Queries/GetProductListQuery.cs` | List products query (cacheable) |
| `Modules/Products/Queries/GetProductByIdQuery.cs` | Get single product query |
| `Modules/Products/DTOs/ProductDto.cs` | Product response shape |
| `Modules/Products/Mappings/ProductProfile.cs` | AutoMapper: Product ↔ DTO/Command |
| `Modules/Auth/Commands/RegisterCommand.cs` | Register command + handler |
| `Modules/Auth/Commands/LoginCommand.cs` | Login command + handler |
| `Modules/Auth/DTOs/AuthResponseDto.cs` | Auth response shape |
| `Modules/Auth/Mappings/AuthMappingProfile.cs` | AutoMapper: RegisterCommand → ApplicationUser |
| `Modules/Tenants/Commands/ProvisionTenantCommand.cs` | Tenant provisioning command + handler |

### eSale.Domain
| File | Purpose |
|------|---------|
| `Common/BaseEntity.cs` | Base class: Id, TenantId, CreatedAt, UpdatedAt |
| `Common/Interfaces/IGenericRepository.cs` | Generic CRUD interface |
| `Common/Interfaces/IUnitOfWork.cs` | Transaction commit interface |
| `Modules/Products/Entities/Product.cs` | Product entity |
| `Modules/Products/Interfaces/IProductRepository.cs` | Product-specific repository |
| `Modules/Auth/Entities/ApplicationUser.cs` | Identity user (FirstName, LastName) |
| `Modules/Tenants/Entities/Tenant.cs` | Tenant registry entity |
| `Modules/Tenants/Interfaces/ITenantRepository.cs` | Tenant CRUD interface |

### eSale.Infrastructure
| File | Purpose |
|------|---------|
| `DependencyInjection.cs` | Registers all infrastructure services |
| `Persistence/AppDbContext.cs` | Tenant-scoped IdentityDbContext + Products |
| `Persistence/CentralDbContext.cs` | Central DB: Tenants only |
| `Persistence/UnitOfWork.cs` | Calls AppDbContext.SaveChangesAsync |
| `Persistence/Repositories/GenericRepository.cs` | Generic EF CRUD implementation |
| `Persistence/TenantConnectionResolver.cs` | Tenant ID → MySQL connection string |
| `Persistence/TenantDbInitializer.cs` | Creates tenant databases + schema |
| `Persistence/DbInitializer.cs` | Creates central database schema |
| `Persistence/Configurations/ProductConfiguration.cs` | EF fluent config for Products table |
| `Persistence/Configurations/TenantConfiguration.cs` | EF fluent config for Tenants table |
| `Persistence/AppDbContextFactory.cs` | Design-time factory for EF migrations |
| `Persistence/CentralDbContextFactory.cs` | Design-time factory for central migrations |
| `Modules/Products/ProductRepository.cs` | Product-specific queries |
| `Modules/Auth/JwtTokenService.cs` | JWT generation with tenantId claim |
| `Modules/Tenants/TenantRepository.cs` | Central DB tenant queries |
| `Caching/RedisCacheService.cs` | Redis implementation with graceful degradation |
| `BackgroundJobs/EmailJobService.cs` | Hangfire email job |
