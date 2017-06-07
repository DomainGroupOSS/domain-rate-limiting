using System;
using StackExchange.Redis;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.RateLimiting.Core;

namespace Domain.RateLimiting.Redis
{
    /// <summary>
    /// Redis implementation for storing and expiring request rate limit values
    /// </summary>
    public class RedisFixedWindowRateLimiter : RedisRateLimiter
    {
        private static readonly IDictionary<RateLimitUnit, Func<DateTime, string>> RateLimitTypeCacheKeyFormatMapping = new Dictionary<RateLimitUnit, Func<DateTime, string>>
        {
            {RateLimitUnit.PerSecond, dateTime => dateTime.ToString("yyyyMMddHHmmss")},
            {RateLimitUnit.PerMinute, dateTime => dateTime.ToString("yyyyMMddHHmm")},
            {RateLimitUnit.PerHour, dateTime => dateTime.ToString("yyyyMMddHH")},
            {RateLimitUnit.PerDay, dateTime => dateTime.ToString("yyyyMMdd")},
        };


        /// <summary>
        /// 
        /// </summary>
        /// <param name="redisEndpoint"></param>
        /// <param name="onException"></param>
        /// <param name="onThrottled"></param>
        /// <param name="connectionTimeout"></param>
        /// <param name="syncTimeout"></param>
        /// <param name="countThrottledRequests"></param>
        /// <param name="circuitBreaker"></param>
        public RedisFixedWindowRateLimiter(string redisEndpoint,
            Action<Exception> onException = null,
            Action<RateLimitingResult> onThrottled = null,
            int connectionTimeout = 2000,
            int syncTimeout = 1000,
            bool countThrottledRequests = false,
            ICircuitBreaker circuitBreaker = null) : base(redisEndpoint,
            onException,
            onThrottled,
            connectionTimeout,
            syncTimeout,
            countThrottledRequests,
            circuitBreaker)
        {
        }

        protected override Task<long> GetNumberOfRequestsAsync(string requestId, string method, string host,
            string routeTemplate,
            RateLimitPolicy policy,
            IList<RateLimitCacheKey> cacheKeys,
            ITransaction redisTransaction, long utcNowTicks)
        {
            RateLimitCacheKey cacheKey =
                new RateLimitCacheKey(requestId, method, host, routeTemplate, policy, RateLimitTypeCacheKeyFormatMapping[policy.Unit]);

            var cacheKeyString = cacheKey.ToString();
            cacheKeys.Add(cacheKey);
            var getKeyTask = redisTransaction.StringGetAsync(cacheKeyString);
            var incrTask = redisTransaction.StringIncrementAsync(cacheKeyString);
            var expireTask = redisTransaction.KeyExpireAsync(cacheKeyString, cacheKey.Expiration);
            return incrTask;
        }

        protected override void UndoUnsuccessfulRequestCount(ITransaction postViolationTransaction, RateLimitCacheKey cacheKey, long utcNowTicks)
        {
            // do nothing
        }

        protected override Task<SortedSetEntry[]> SetupGetOldestRequestTimestampInTicks(ITransaction postViolationTransaction,
            RateLimitCacheKey cacheKey, long utcNowTicks)
        {
            // do nothing
            return new Task<SortedSetEntry[]>(() => null);
        }

        protected override Task<long> GetOldestRequestTimestampInTicks(Task<SortedSetEntry[]> task, RateLimitCacheKey cacheKey, long utcNowTicks)
        {
            return Task.FromResult(Trim(utcNowTicks, (long)cacheKey.Unit));
        }

        public long Trim(long dateTimeInTicks, long ticksPerUnit)
        {
            return dateTimeInTicks - (dateTimeInTicks % ticksPerUnit);
        }
    }
}

