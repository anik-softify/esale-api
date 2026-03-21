using eSale.Application.Common.Caching;
using eSale.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace eSale.Application.Common.Behaviors;

public sealed class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ICacheService _cacheService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(
        ICacheService cacheService,
        ITenantProvider tenantProvider,
        ILogger<CachingBehavior<TRequest, TResponse>> logger)
    {
        _cacheService = cacheService;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICacheableQuery cacheableQuery)
        {
            return await next();
        }

        var tenantId = _tenantProvider.GetTenantId();
        var scopedKey = $"tenant:{tenantId}:{cacheableQuery.CacheKey}";

        var cachedResponse = await _cacheService.GetAsync<TResponse>(scopedKey, cancellationToken);
        if (cachedResponse is not null)
        {
            _logger.LogInformation("Cache hit for key {CacheKey}", scopedKey);
            return cachedResponse;
        }

        _logger.LogInformation("Cache miss for key {CacheKey}", scopedKey);
        var response = await next();
        await _cacheService.SetAsync(scopedKey, response, cacheableQuery.Expiration, cancellationToken);
        return response;
    }
}
