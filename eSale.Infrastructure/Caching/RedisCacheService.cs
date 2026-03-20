using System.Text.Json;
using eSale.Application.Common.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace eSale.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IDistributedCache distributedCache, ILogger<RedisCacheService> logger)
    {
        _distributedCache = distributedCache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = await _distributedCache.GetStringAsync(key, cancellationToken);
            return payload is null ? default : JsonSerializer.Deserialize<T>(payload, SerializerOptions);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            _logger.LogWarning(exception, "Redis is unavailable while reading key {CacheKey}. Falling back to database.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(value, SerializerOptions);
            await _distributedCache.SetStringAsync(
                key,
                payload,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration
                },
                cancellationToken);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            _logger.LogWarning(exception, "Redis is unavailable while writing key {CacheKey}. Continuing without cache.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _distributedCache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            _logger.LogWarning(exception, "Redis is unavailable while removing key {CacheKey}.", key);
        }
    }

    private static bool IsRedisFailure(Exception exception) =>
        exception is RedisConnectionException
        or RedisTimeoutException
        or RedisServerException
        or TimeoutException;
}
