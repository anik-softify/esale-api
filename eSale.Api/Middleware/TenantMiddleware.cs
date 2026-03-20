namespace eSale.Api.Middleware;

/// <summary>
/// Extracts TenantId from the "X-Tenant-Id" request header and stores it
/// in HttpContext.Items so the TenantProvider can read it.
///
/// In production you'd also validate the tenant exists and the user has access.
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantHeader)
            || !Guid.TryParse(tenantHeader, out var tenantId))
        {
            throw new BadHttpRequestException("Missing or invalid X-Tenant-Id header.");
        }

        context.Items["TenantId"] = tenantId;

        await _next(context);
    }
}
