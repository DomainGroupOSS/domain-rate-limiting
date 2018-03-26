using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.RateLimiting.Core;

namespace Domain.RateLimiting.Redis
{
    /// <summary>
    /// Redis implementation for storing and expiring request rate limit values using a sliding window expiry policy which
    /// avoids the problems of occasional bursts at interval boundaries
    /// </summary>
    public class RedisSlidingWindowRateLimiter : RedisRateLimiter
    {
        private static readonly IDictionary<RateLimitUnit, Func<DateTime, string>> RateLimitTypeCacheKeyFormatMapping = new Dictionary<RateLimitUnit, Func<DateTime, string>>
        {
            {RateLimitUnit.PerSecond, _ => RateLimitUnit.PerSecond.ToString()},
            {RateLimitUnit.PerMinute, _ => RateLimitUnit.PerMinute.ToString()},
            {RateLimitUnit.PerHour, _ => RateLimitUnit.PerHour.ToString()},
            {RateLimitUnit.PerDay, _ => RateLimitUnit.PerDay.ToString()},
            {RateLimitUnit.PerCustomPeriod, _ => throw new NotSupportedException("Custom Period is not supported by Sliding window yet")}
        };

        public RedisSlidingWindowRateLimiter(string redisEndpoint,
            Action<Exception> onException = null,
            Action<RateLimitingResult> onThrottled = null,
            int connectionTimeout = 2000,
            int syncTimeout = 1000,
            bool countThrottledRequests = false,
            ICircuitBreaker circuitBreaker = null,
            IClock clock = null,
            Func<Task<IConnectionMultiplexer>> connectToRedisFunc = null) : base(redisEndpoint,
            onException,
            onThrottled,
            connectionTimeout,
            syncTimeout,
            countThrottledRequests,
            circuitBreaker,
            clock,
            connectToRedisFunc)
        {
        }

        protected override Task<long> GetNumberOfRequestsAsync(string requestId, string method, string host, string routeTemplate,
             AllowedCallRate allowedCallRate, IList<RateLimitCacheKey> cacheKeys, 
             ITransaction redisTransaction, long utcNowTicks, int costPerCall = 1)
        {
            if (costPerCall != 1)
                throw new ArgumentOutOfRangeException("Only cost of value 1 is currently supported by the sliding window rate limiter");

            var cacheKey =
                new RateLimitCacheKey(requestId, method, host, routeTemplate, allowedCallRate, 
                RateLimitTypeCacheKeyFormatMapping[allowedCallRate.Unit]);

            var cacheKeyString = cacheKey.ToString();
            cacheKeys.Add(cacheKey);
            
            var sortedSetRemoveRangeByScoreAsync = redisTransaction.SortedSetRemoveRangeByScoreAsync(
                cacheKeyString, 0, utcNowTicks - (long)cacheKey.Unit);

            var sortedSetAddAsync = redisTransaction.SortedSetAddAsync(cacheKeyString, Guid.NewGuid().ToString(), utcNowTicks);
            var numberOfRequestsInWindowAsyncList = redisTransaction.SortedSetLengthAsync(cacheKeyString);
            var expireTask = redisTransaction.KeyExpireAsync(cacheKeyString,
                cacheKey.Expiration.Add(new TimeSpan(0, 1, 0)));

            return numberOfRequestsInWindowAsyncList;
        }

        protected override void UndoUnsuccessfulRequestCount(ITransaction postViolationTransaction, RateLimitCacheKey cacheKey, long now, int costPerCall = 1)
        {
            if (costPerCall != 1)
                throw new NotSupportedException("Only cost of value 1 is currently supported by the sliding window rate limiter");

            var adjust = postViolationTransaction.SortedSetRemoveRangeByRankAsync(cacheKey.ToString(), -1, -1);
        }

        protected override Task<SortedSetEntry[]> SetupGetOldestRequestTimestampInTicks(ITransaction postViolationTransaction,
            RateLimitCacheKey cacheKey, long utcNowTicks)
        {
            return postViolationTransaction.SortedSetRangeByRankWithScoresAsync(cacheKey.ToString(), 0, 0);
        }

        protected override async Task<long> GetOldestRequestTimestampInTicks(Task<SortedSetEntry[]> task, RateLimitCacheKey cacheKey, long utcNowTicks)
        {
            var t = await task.ConfigureAwait(false);
            return (long)t[0].Score;
        }
    }
}
