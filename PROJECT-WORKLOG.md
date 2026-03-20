# eSale Worklog

Last updated: 2026-03-20 14:21:39 +06:00

## Overview

This repository contains the backend API for the `eSale` project. The solution follows a Clean Architecture structure with these layers:

- `eSale.Api`
- `eSale.Application`
- `eSale.Domain`
- `eSale.Infrastructure`
- `eSale.Tests`

The backend is now prepared for a more production-ready workflow with Docker, MySQL, Redis, Hangfire, Serilog, Seq, and multi-container local execution.

## Major Work Completed

### 1. Clean Architecture Upgrades

- Added a generic repository contract and infrastructure implementation.
- Added CQRS examples with MediatR for product creation and product retrieval.
- Added AutoMapper mappings for `Product` and `ProductDto`.
- Added FluentValidation and MediatR pipeline behaviors.
- Added centralized exception handling middleware.
- Added unit test example for command handler flow.

### 2. Infrastructure Improvements

- Switched MySQL provider path to Pomelo-compatible setup.
- Kept the app on `.NET 10` while using `EF Core 9` and `Pomelo 9` for compatibility.
- Added Redis cache support with graceful degradation behavior.
- Added Hangfire with MySQL storage.
- Added database initialization support for runtime startup.

### 3. Docker and Deployment

- Added a multi-stage Dockerfile for the API.
- Added `docker-compose.yml` for:
  - `api1`
  - `api2`
  - `mysql`
  - `redis`
  - `seq`
  - `nginx`
- Added Nginx reverse proxy configuration for load balancing.
- Added `.dockerignore`.
- Added `.env.example`.
- Added deployment notes in `README-DEPLOYMENT.md`.

### 4. Logging and Observability

- Configured Serilog for console, file, and Seq logging.
- Verified local Seq access on `http://localhost:5341`.

### 5. Startup Stability Fixes

- Fixed startup behavior so missing HTTP context does not break non-request startup operations.
- Added configuration flags for controlled startup responsibilities:
  - `Infrastructure__RunDbInitialization`
  - `Infrastructure__RunHangfireServer`
- Configured Docker so:
  - `api1` performs DB initialization and runs Hangfire server
  - `api2` only serves API traffic
- Reduced multi-replica startup conflicts.

### 6. Schema Bootstrap Fix

- Added application schema bootstrap logic for `Products` when EF migrations are not present.
- Verified the API now starts and responds correctly to product list requests with a tenant header.

## Current Runtime Status

The Docker stack was verified successfully in local development.

### Working Endpoints

- API through Nginx: `http://localhost:8080`
- Seq: `http://localhost:5341`
- MySQL host port: `3307`
- Redis host port: `6379`

### Important Request Requirement

The API requires the `X-Tenant-Id` header. Without it, requests are rejected by tenant middleware with `400 Bad Request`.

Example:

```powershell
curl -H "X-Tenant-Id: 11111111-1111-1111-1111-111111111111" http://localhost:8080/api/products
```

Expected current response:

```json
[]
```

This means:

- the API is running
- Nginx is forwarding traffic correctly
- MySQL is reachable
- the `Products` endpoint is working
- there is no product data yet

## Important Compatibility Notes

- Runtime target remains `net10.0`.
- EF Core packages were aligned to `9.x`.
- Pomelo package was aligned to `9.0.0`.
- This combination was chosen because Pomelo `10.x` was not available in the tested restore path.

## Known Non-Blocking Warnings

- Requests without `X-Tenant-Id` return `400`, which is expected.
- `UseHttpsRedirection` may log a warning in containerized local HTTP-only runs.
- MediatR license warning appears in logs for development/testing.
- `Newtonsoft.Json 11.0.2` vulnerability warning appears during Docker publish through a dependency chain.

## Frontend Direction

A separate frontend repository approach was chosen.

Recommended repo structure:

- backend repo: `eSale`
- frontend repo: `eSale-web`

A separate folder was created outside this repository:

- `E:\eSale-web`

Planned frontend stack:

- `Next.js`
- `TypeScript`

This supports clean separation between backend and frontend teams.

## Helpful Commands

### Start local stack

```powershell
docker compose up --build -d
```

### Stop local stack

```powershell
docker compose down
```

### Check running containers

```powershell
docker compose ps
```

### Test product list with tenant header

```powershell
curl -H "X-Tenant-Id: 11111111-1111-1111-1111-111111111111" http://localhost:8080/api/products
```

## Suggested Next Steps

- Add real EF Core migrations instead of relying on bootstrap SQL for application tables.
- Add authentication and authorization.
- Add health checks for API, MySQL, and Redis.
- Add OpenTelemetry.
- Add product create/update/delete end-to-end tests.
- Initialize the separate `eSale-web` frontend repository with Next.js.
