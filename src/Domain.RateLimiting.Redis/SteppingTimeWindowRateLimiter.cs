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
    public class SteppingTimeWindowRateLimiter : RedisRateLimiter
    {
        private static readonly IDictionary<RateLimitUnit, Func<AllowedConsumptionRate, Func<DateTime, string>>> RateLimitTypeCacheKeyFormatMapping =
            new Dictionary<RateLimitUnit, Func<AllowedConsumptionRate, Func<DateTime, string>>>
        {
            {RateLimitUnit.PerSecond, allowedCallRate => dateTime => dateTime.ToString("yyyyMMddHHmmss")},
            {RateLimitUnit.PerMinute, allowedCallRate => dateTime => dateTime.ToString("yyyyMMddHHmm")},
            {RateLimitUnit.PerHour, allowedCallRate => dateTime => dateTime.ToString("yyyyMMddHH")},
            {RateLimitUnit.PerDay, allowedCallRate => dateTime => dateTime.ToString("yyyyMMdd")},
            {RateLimitUnit.PerCustomPeriod, allowedCallRate => dateTime =>
                {
                    GetDateRange(allowedCallRate, dateTime, out DateTime fromUtc, out DateTime toUtc);
                    return $"{fromUtc.ToString("yyyyMMddHHmmss")}::{toUtc.ToString("yyyyMMddHHmmss")}";
                }
            }
        };
        
        public SteppingTimeWindowRateLimiter(string redisEndpoint,
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

        protected override Func<long> GetNumberOfRequestsAsync(string requestId, string method, string host,
            string routeTemplate,
            AllowedConsumptionRate allowedCallRate,
            IList<RateLimitCacheKey> cacheKeys,
            ITransaction redisTransaction, long utcNowTicks, int costPerCall = 1)
        {
            RateLimitCacheKey cacheKey =
               new RateLimitCacheKey(requestId, method, host, routeTemplate, allowedCallRate,
               RateLimitTypeCacheKeyFormatMapping[allowedCallRate.Unit].Invoke(allowedCallRate));

            var cacheKeyString = cacheKey.ToString();
            cacheKeys.Add(cacheKey);
            
            var incrTask = redisTransaction.StringIncrementAsync(cacheKeyString, costPerCall);
            redisTransaction.KeyExpireAsync(cacheKeyString, cacheKey.Expiration);
            return () => incrTask.Result;
        }

        protected override void UndoUnsuccessfulRequestCount(ITransaction postViolationTransaction, RateLimitCacheKey cacheKey, long utcNowTicks, 
            int costPerCall = 1)
        {
            postViolationTransaction.StringDecrementAsync(cacheKey.ToString(), costPerCall);
        }

        public long Trim(long dateTimeInTicks, AllowedConsumptionRate allowedCallRate)
        {
            return dateTimeInTicks - (dateTimeInTicks % GetTicksPerUnit(allowedCallRate));
        }

        protected override Func<long> GetOldestRequestTimestampInTicksFunc(ITransaction postViolationTransaction, RateLimitCacheKey cacheKey, long utcNowTicks)
        {
            return () => Trim(utcNowTicks, cacheKey.AllowedConsumptionRate);
        }
    }
}

