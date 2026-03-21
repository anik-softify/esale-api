# Current Database-Per-Tenant Notes

## Current Model

`eSale-api` now uses:

- `CentralDbContext` for tenant registry and shared platform metadata
- `AppDbContext` per tenant database for business data and tenant-scoped identity

This means:

- each tenant has its own MySQL database
- each tenant database contains:
  - ASP.NET Identity tables
  - business tables such as `Products`
- the central database contains:
  - `Tenants`
  - Hangfire data
  - platform-level shared metadata

## Request Flow

1. request enters the API
2. [TenantMiddleware.cs](e:\eSale\eSale-api\eSale.Api\Middleware\TenantMiddleware.cs) resolves `TenantId`
3. the same middleware resolves the tenant connection string
4. `TenantId` and `TenantConnectionString` are stored in `HttpContext.Items`
5. [TenantProvider.cs](e:\eSale\eSale-api\eSale.Api\Middleware\TenantProvider.cs) exposes them to the application and infrastructure layers
6. [AppDbContext.cs](e:\eSale\eSale-api\eSale.Infrastructure\Persistence\AppDbContext.cs) is created with the tenant database connection
7. product and auth queries run against the tenant database

## Auth Model

Identity is tenant-scoped:

- [ApplicationUser.cs](e:\eSale\eSale-api\eSale.Domain\Modules\Auth\Entities\ApplicationUser.cs)
- [AppDbContext.cs](e:\eSale\eSale-api\eSale.Infrastructure\Persistence\AppDbContext.cs)
- [DependencyInjection.cs](e:\eSale\eSale-api\eSale.Infrastructure\DependencyInjection.cs)

Important result:

- `RequireUniqueEmail = true` is enforced inside each tenant database
- the same email can exist in different tenants
- login and registration are tenant-aware because they run against the current tenant database

## Provisioning Flow

Tenant provisioning uses:

- [ProvisionTenantCommand.cs](e:\eSale\eSale-api\eSale.Application\Modules\Tenants\Commands\ProvisionTenantCommand.cs)
- [TenantRepository.cs](e:\eSale\eSale-api\eSale.Infrastructure\Modules\Tenants\TenantRepository.cs)
- [TenantDbInitializer.cs](e:\eSale\eSale-api\eSale.Infrastructure\Persistence\TenantDbInitializer.cs)

Current provisioning responsibilities:

- create a tenant record in the central database
- derive a tenant database name
- create the tenant database if it does not exist
- ensure tenant schema is created

## Remaining SaaS Foundation Work

These are still worth adding later:

1. `TenantSlug` or subdomain-based tenant resolution
2. tenant invitation and onboarding flow
3. branch support under a tenant
4. tenant billing and plan metadata
5. per-tenant audit logging
6. tenant database migration strategy beyond `EnsureCreated`
7. tenant suspension and deactivation handling
