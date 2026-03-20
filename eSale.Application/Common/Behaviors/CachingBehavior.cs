using eSale.Application.Common.Caching;
using MediatR;
using Microsoft.Extensions.Logging;

namespace eSale.Application.Common.Behaviors;

public sealed class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(
        ICacheService cacheService,
        ILogger<CachingBehavior<TRequest, TResponse>> logger)
    {
        _cacheService = cacheService;
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

        var cachedResponse = await _cacheService.GetAsync<TResponse>(cacheableQuery.CacheKey, cancellationToken);
        if (cachedResponse is not null)
        {
            _logger.LogInformation("Cache hit for key {CacheKey}", cacheableQuery.CacheKey);
            return cachedResponse;
        }

        _logger.LogInformation("Cache miss for key {CacheKey}", cacheableQuery.CacheKey);
        var response = await next();
        await _cacheService.SetAsync(cacheableQuery.CacheKey, response, cacheableQuery.Expiration, cancellationToken);
        return response;
    }
}
