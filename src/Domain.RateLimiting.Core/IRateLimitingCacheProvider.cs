using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.RateLimiting.Core
{
    /// <summary>
    /// Provides access to a cache provider which caches requests limiting values
    /// </summary>
    public interface IRateLimitingCacheProvider
    {
        /// <summary>
        /// Rate limits a request using to cache key provided
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <returns>true if the request has reached the max rate limit, false otherwise</returns>
        Task<RateLimitingResult> LimitRequestAsync(RateLimitCacheKey cacheKey);

        Task<RateLimitingResult> LimitRequestAsync(string requestId, string method, string host, string routeTemplate,
            IList<RateLimitPolicy> rateLimitPolicies);
    }
}