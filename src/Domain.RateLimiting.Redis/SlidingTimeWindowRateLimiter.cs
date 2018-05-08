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
    public class SlidingTimeWindowRateLimiter : RedisRateLimiter
    {
        private static readonly IDictionary<RateLimitUnit, Func<AllowedConsumptionRate, Func<DateTime, string>>> RateLimitTypeCacheKeyFormatMapping =
            new Dictionary<RateLimitUnit, Func<AllowedConsumptionRate, Func<DateTime, string>>>
        {
            {RateLimitUnit.PerSecond, allowedCallrate => _ => RateLimitUnit.PerSecond.ToString()},
            {RateLimitUnit.PerMinute, allowedCallrate => _ => RateLimitUnit.PerMinute.ToString()},
            {RateLimitUnit.PerHour, allowedCallrate => _ => RateLimitUnit.PerHour.ToString()},
            {RateLimitUnit.PerDay, allowedCallrate => _ => RateLimitUnit.PerDay.ToString()},
            {RateLimitUnit.PerCustomPeriod, allowedCallRate => _ =>
                {
                    //return $"{allowedCallRate.Period.StartDateTimeUtc.ToString("yyyyMMddHHmmss")}::{allowedCallRate.Period.Duration.TotalSeconds}";
                    throw new NotSupportedException("Custom Period is NOT currently supported by the sliding time window rate limiter. Consider using the SteppingTimeWindowRateLimiter.");
                }
            }
       };

        public SlidingTimeWindowRateLimiter(string redisEndpoint,
            Action<Exception> onException = null,
            Action<RateLimitingResult> onThrottled = null,
            int connectionTimeoutInMilliseconds = 2000,
            int syncTimeoutInMilliseconds = 1000,
            bool countThrottledRequests = false,
            ICircuitBreaker circuitBreaker = null,
            IClock clock = null,
            Func<Task<IConnectionMultiplexer>> connectToRedisFunc = null) : base(redisEndpoint,
            onException,
            onThrottled,
            connectionTimeoutInMilliseconds,
            syncTimeoutInMilliseconds,
            countThrottledRequests,
            circuitBreaker,
            clock,
            connectToRedisFunc)
        {
        }

        protected override Task<long> GetNumberOfRequestsAsync(string requestId, string method, string host, string routeTemplate,
             AllowedConsumptionRate allowedCallRate, IList<RateLimitCacheKey> cacheKeys, 
             ITransaction redisTransaction, long utcNowTicks, int costPerCall = 1)
        {
            if (costPerCall != 1)
                throw new ArgumentOutOfRangeException("Only cost of value 1 is currently supported by the sliding window rate limiter");

            var cacheKey =
                new RateLimitCacheKey(requestId, method, host, routeTemplate, allowedCallRate,
                RateLimitTypeCacheKeyFormatMapping[allowedCallRate.Unit].Invoke(allowedCallRate));

            var cacheKeyString = cacheKey.ToString();
            cacheKeys.Add(cacheKey);
            
            var sortedSetRemoveRangeByScoreAsync = redisTransaction.SortedSetRemoveRangeByScoreAsync(
                cacheKeyString, 0, utcNowTicks - GetTicksPerUnit(cacheKey.AllowedConsumptionRate));

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

        protected override Func<long> GetOldestRequestTimestampInTicksFunc(ITransaction postViolationTransaction, 
            RateLimitCacheKey cacheKey, long utcNowTicks)
        {
            var task = postViolationTransaction.SortedSetRangeByRankWithScoresAsync(cacheKey.ToString(), 0, 0);
            return () =>
            {
                var sortedSetEntries = task.Result;

                if (sortedSetEntries == null || sortedSetEntries.Length == 0)
                    return utcNowTicks - GetTicksPerUnit(cacheKey.AllowedConsumptionRate);

                return (long)sortedSetEntries[0].Score;
            };
        }
    }
}
