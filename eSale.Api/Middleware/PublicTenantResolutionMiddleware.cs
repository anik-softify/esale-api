using System.Security.Claims;
using eSale.Application.Common.Interfaces;

namespace eSale.Api.Middleware;

/// <summary>
/// Resolves the current tenant for both public auth routes and protected API routes.
/// Tenant identity can come from header or, for authenticated requests, the JWT claim.
/// </summary>
public sealed class PublicTenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly string[] ExcludedPrefixes =
    [
        "/hangfire"
    ];

    public PublicTenantResolutionMiddleware(RequestDelegate next)
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

        var tenantId = ResolveTenantId(context);
        context.Items["TenantId"] = tenantId;

        await _next(context);
    }

    private static Guid ResolveTenantId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader)
            && Guid.TryParse(tenantHeader, out var headerTenantId))
        {
            return headerTenantId;
        }

        var claimValue = context.User.FindFirstValue("tenantId");
        if (Guid.TryParse(claimValue, out var claimTenantId))
        {
            return claimTenantId;
        }

        throw new BadHttpRequestException("Missing or invalid tenant identifier.");
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
