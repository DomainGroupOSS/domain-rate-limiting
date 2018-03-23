using System;
using StackExchange.Redis;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.RateLimiting.Core;

namespace Domain.RateLimiting.Redis
{
    public class RedisQuotaLimiter : RedisRateLimiter
    {
        public RedisQuotaLimiter(
            Func<string ,string,string,string,Task<int>> costFunction,
            string redisEndpoint, 
            Action<Exception> onException = null, 
            Action<RateLimitingResult> onThrottled = null, 
            int connectionTimeout = 2000, 
            int syncTimeout = 1000, 
            bool countThrottledRequests = false, 
            ICircuitBreaker circuitBreaker = null, 
            IClock clock = null, 
            Func<Task<IConnectionMultiplexer>> connectToRedisFunc = null) : base(redisEndpoint, onException, onThrottled, connectionTimeout, syncTimeout, countThrottledRequests, circuitBreaker, clock, connectToRedisFunc)
        {
        }

        protected override Task<long> GetNumberOfRequestsAsync(string requestId, string method, string host, string routeTemplate, AllowedCallRate policy, 
            IList<RateLimitCacheKey> cacheKeys, ITransaction redisTransaction, long utcNowTicks)
        {
            throw new NotImplementedException();
        }

        protected override Task<long> GetOldestRequestTimestampInTicks(Task<SortedSetEntry[]> task, RateLimitCacheKey cacheKey, long utcNowTicks)
        {
            throw new NotImplementedException();
        }

        protected override Task<SortedSetEntry[]> SetupGetOldestRequestTimestampInTicks(ITransaction postViolationTransaction, RateLimitCacheKey cacheKey, long utcNowTicks)
        {
            throw new NotImplementedException();
        }

        protected override void UndoUnsuccessfulRequestCount(ITransaction postViolationTransaction, RateLimitCacheKey cacheKey, long utcNowTicks)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Redis implementation for storing and expiring request rate limit values
    /// </summary>
    public class RedisFixedWindowRateLimiter : RedisRateLimiter
    {
        private static readonly IDictionary<RateLimitUnit, Func<AllowedCallRate, Func<DateTime, string>>> RateLimitTypeCacheKeyFormatMapping =
            new Dictionary<RateLimitUnit, Func<AllowedCallRate, Func<DateTime, string>>>
        {
            {RateLimitUnit.PerSecond, allowedCallRate => dateTime => dateTime.ToString("yyyyMMddHHmmss")},
            {RateLimitUnit.PerMinute, allowedCallRate => dateTime => dateTime.ToString("yyyyMMddHHmm")},
            {RateLimitUnit.PerHour, allowedCallRate => dateTime => dateTime.ToString("yyyyMMddHH")},
            {RateLimitUnit.PerDay, allowedCallRate => dateTime => dateTime.ToString("yyyyMMdd")},
            {RateLimitUnit.PerCustomPeriod, allowedCallRate => dateTime =>
                {
                    GetDateRange(allowedCallRate,dateTime, out DateTime from, out DateTime to);
                    return $"{from.ToString("yyyyMMddHHmmss")}:{to.ToString("yyyyMMddHHmmss")}";
                }
            }
        };

        private static void GetDateRange(AllowedCallRate allowedCallRate, DateTime dateTime, out DateTime from, out DateTime to)
        {
            var periodUnits = allowedCallRate.Period.Rolling ?
                                    Math.Floor(dateTime.Subtract(allowedCallRate.Period.StartDate).TotalSeconds / allowedCallRate.Period.Duration.TotalSeconds) : 0;

            from = allowedCallRate.Period.StartDate.Add(
                new TimeSpan(0, 0, Convert.ToInt32(allowedCallRate.Period.Duration.TotalSeconds * periodUnits)));
            to = from.Add(allowedCallRate.Period.Duration);
        }

        public Func<string, string, string, string, Task<int>> CostFunction { get; }

        public RedisFixedWindowRateLimiter(string redisEndpoint,
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

        protected override Task<long> GetNumberOfRequestsAsync(string requestId, string method, string host,
            string routeTemplate,
            AllowedCallRate allowedCallRate,
            IList<RateLimitCacheKey> cacheKeys,
            ITransaction redisTransaction, long utcNowTicks)
        {
            RateLimitCacheKey cacheKey =
               new RateLimitCacheKey(requestId, method, host, routeTemplate, allowedCallRate,
               RateLimitTypeCacheKeyFormatMapping[allowedCallRate.Unit].Invoke(allowedCallRate));

            var cacheKeyString = cacheKey.ToString();
            cacheKeys.Add(cacheKey);

            if (allowedCallRate.Unit == RateLimitUnit.PerCustomPeriod)
            {
                GetDateRange(allowedCallRate, DateTime.Now, out DateTime from, out DateTime to);
                if (!(DateTime.Now >= from && DateTime.Now <= to))
                    return redisTransaction.StringIncrementAsync(cacheKeyString, allowedCallRate.Limit + 10);
            }
           

            var incrTask = redisTransaction.StringIncrementAsync(cacheKeyString, allowedCallRate.Cost);
            var getKeyTask = redisTransaction.StringGetAsync(cacheKeyString);
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

