# Database-per-Tenant Architecture

## Architecture Overview

```
┌─────────────────────────────────┐
│        MySQL Instance           │
├─────────────────────────────────┤
│  esale_central                  │  ← Tenant registry + Hangfire only
│    ├── Tenants                  │
│    └── Hangfire tables          │
├─────────────────────────────────┤
│  esale_tenant_acme              │  ← Tenant A (fully isolated)
│    ├── AspNetUsers              │
│    ├── AspNetRoles              │
│    ├── AspNetUserRoles          │
│    └── Products                 │
├─────────────────────────────────┤
│  esale_tenant_globex            │  ← Tenant B (fully isolated)
│    ├── AspNetUsers              │
│    ├── AspNetRoles              │
│    ├── AspNetUserRoles          │
│    └── Products                 │
└─────────────────────────────────┘
```

**Key design decision**: Identity (ASP.NET Identity) lives inside each tenant database, not in the central database. This means:
- Each tenant has its own users, roles, and permissions
- Same email can exist in different tenants (true isolation)
- No cross-tenant user data leakage

## Request Flow

```
Request with X-Tenant-Id header (or JWT tenantId claim)
  → Authentication middleware (validates JWT, populates claims)
  → TenantMiddleware
    → Resolves tenant ID from header OR JWT claim
    → Cross-validates: if both present, they must match
    → ITenantConnectionResolver looks up Tenant in central DB → derives connection string
    → Stores TenantId + ConnectionString in HttpContext.Items
  → TenantProvider reads from HttpContext.Items
  → AppDbContext (IdentityDbContext) created with dynamic connection string
  → UserManager, ProductRepository, etc. all hit the tenant-specific database
```

## Database Contexts

| Context | Database | Contains | Registration |
|---------|----------|----------|-------------|
| CentralDbContext | esale_central | Tenants, Hangfire | Static connection (AddDbContext) |
| AppDbContext | esale_tenant_X | Identity + Products | Scoped factory (dynamic connection) |

## Tenant Resolution

TenantMiddleware resolves tenant from two sources (in priority order):

1. `X-Tenant-Id` HTTP header (for unauthenticated requests: register, login)
2. `tenantId` JWT claim (for authenticated requests)

If both are present and don't match → `UnauthorizedAccessException`.

### Excluded routes (no tenant needed)

- `POST /api/tenants/provision` — creates a new tenant
- `/hangfire` — background job dashboard

### All other routes require a tenant (including auth)

- `POST /api/account/register` — requires X-Tenant-Id header
- `POST /api/account/login` — requires X-Tenant-Id header
- `GET /api/products` — uses header or JWT claim
- `POST /api/products` — uses header or JWT claim

## Safety Guarantees

1. **No silent fallback**: If `AppDbContext` is resolved without a tenant connection string, it throws `InvalidOperationException` immediately
2. **No cross-tenant data**: Each tenant database is physically separate
3. **No background job leaks**: `TenantProvider` throws if there's no HTTP context — background jobs must use explicit `StartupTenantProvider`
4. **JWT cross-validation**: Header tenant must match token tenant

## Files

| File | Purpose |
|------|---------|
| `Tenant.cs` | Tenant registry entity (central DB) |
| `ITenantRepository.cs` | Repository interface for tenant CRUD |
| `ITenantConnectionResolver.cs` | Resolves tenant ID → connection string |
| `ITenantDbInitializer.cs` | DB initialization interface |
| `CentralDbContext.cs` | Central DB context (Tenants only, no Identity) |
| `AppDbContext.cs` | Tenant DB context (IdentityDbContext + Products) |
| `TenantConnectionResolver.cs` | Looks up tenant → builds MySQL connection string |
| `TenantDbInitializer.cs` | Creates tenant database + runs EnsureCreated |
| `TenantRepository.cs` | Central DB tenant queries |
| `TenantMiddleware.cs` | Resolves tenant from header/JWT per request |
| `TenantProvider.cs` | Reads tenant context from HttpContext.Items |
| `ProvisionTenantCommand.cs` | MediatR command for provisioning new tenants |
| `TenantsController.cs` | `POST /api/tenants/provision` |

## How to Use

```bash
# 1. Provision a new tenant (no X-Tenant-Id needed)
POST /api/tenants/provision
Content-Type: application/json

{"name": "Acme Corp"}
# Returns: tenant ID (a GUID)
# Creates database: esale_tenant_acme_corp

# 2. Register a user in that tenant
POST /api/account/register
X-Tenant-Id: <tenant-id>
Content-Type: application/json

{"firstName": "Anik", "lastName": "Das", "email": "anik@example.com", "password": "MyPassword1"}
# User is created in esale_tenant_acme_corp.AspNetUsers

# 3. Login
POST /api/account/login
X-Tenant-Id: <tenant-id>
Content-Type: application/json

{"email": "anik@example.com", "password": "MyPassword1"}
# Returns JWT with tenantId claim

# 4. Use JWT for authenticated requests (header optional when JWT has tenantId)
GET /api/products
Authorization: Bearer <jwt-token>
# Hits esale_tenant_acme_corp.Products
```
