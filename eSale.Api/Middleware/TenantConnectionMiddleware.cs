using System.Security.Claims;
using eSale.Application.Common.Interfaces;

namespace eSale.Api.Middleware;

/// <summary>
/// Resolves the tenant database connection string for routes that use tenant data.
/// </summary>
public sealed class TenantConnectionMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly string[] ExcludedPrefixes =
    [
        "/api/account",
        "/api/tenants",
        "/hangfire"
    ];

    public TenantConnectionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (IsExcluded(path))
        {
            await _next(context);
            return;
        }

        if (!context.Items.TryGetValue("TenantId", out var tenantObj) || tenantObj is not Guid tenantId)
        {
            throw new InvalidOperationException("TenantId not found in request context.");
        }

        var claimTenant = context.User.FindFirstValue("tenantId");
        if (context.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(claimTenant, out var claimTenantId)
            && claimTenantId != tenantId)
        {
            throw new UnauthorizedAccessException("Authenticated tenant does not match the requested tenant.");
        }

        var resolver = context.RequestServices.GetRequiredService<ITenantConnectionResolver>();
        var connectionString = await resolver.GetConnectionStringAsync(tenantId, context.RequestAborted);

        context.Items["TenantConnectionString"] = connectionString;

        await _next(context);
    }

    private static bool IsExcluded(string path)
    {
        foreach (var prefix in ExcludedPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
