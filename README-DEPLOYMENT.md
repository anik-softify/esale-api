# eSale Deployment Notes

## Local development on Windows

Use `launchSettings.json` for local-only development values. For safer local development, you can also use user secrets or PowerShell environment variables instead of storing credentials in source-controlled files.

Example PowerShell session:

```powershell
$env:ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=eSaleDb_Dev;User=root;Password=your_password;"
dotnet ef migrations add InitialCreate --project eSale.Infrastructure --startup-project eSale.Api
dotnet run --project eSale.Api
```

## Docker

1. Copy `.env.example` to `.env`
2. Update the MySQL passwords
3. Run:

```powershell
docker compose up --build
```

Reverse proxy:

```text
http://localhost:8080
```

Seq:

```text
http://localhost:5341
```

MySQL host from the API containers:

```text
mysql
```

Redis host from the API containers:

```text
redis
```

MySQL host from your local machine:

```text
localhost
```

## Linux hosting

- Keep `ASPNETCORE_ENVIRONMENT=Production`
- Set `ConnectionStrings__DefaultConnection` through environment variables or a secret manager
- Keep the MySQL data directory on a Docker volume
- Mount `/app/logs` if you want file logs persisted outside the container
- Use a reverse proxy such as Nginx for HTTPS termination

## EF Core migrations

The app applies pending migrations automatically on startup.

To create a new migration:

```powershell
$env:ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=eSaleDb_Dev;User=root;Password=your_password;"
dotnet ef migrations add AddProductIndexes --project eSale.Infrastructure --startup-project eSale.Api
```
