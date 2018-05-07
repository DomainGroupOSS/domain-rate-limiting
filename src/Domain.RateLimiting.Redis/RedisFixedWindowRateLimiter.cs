﻿using System;
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

        private static void GetDateRange(AllowedConsumptionRate allowedCallRate, DateTime dateTimeUtc, out DateTime fromUtc, out DateTime toUtc)
        {
            var periodUnits = allowedCallRate.Period.Repeating ?
                                    Math.Floor(dateTimeUtc.Subtract(allowedCallRate.Period.StartDateTimeUtc).TotalHours
                                    / allowedCallRate.Period.Duration.TotalHours) : 0;

            fromUtc = allowedCallRate.Period.StartDateTimeUtc.Add(
                new TimeSpan(Convert.ToInt32(allowedCallRate.Period.Duration.TotalHours * periodUnits), 0, 0));
            toUtc = fromUtc.Add(allowedCallRate.Period.Duration);
        }

        public Func<string, string, string, string, Task<int>> CostFunction { get; }

        public RedisFixedWindowRateLimiter(string redisEndpoint,
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

        protected override Task<long> GetNumberOfRequestsAsync(string requestId, string method, string host,
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

            if (allowedCallRate.Unit == RateLimitUnit.PerCustomPeriod)
            {
                var dateTimeNowUtc = new DateTime(utcNowTicks, DateTimeKind.Utc);
                GetDateRange(allowedCallRate, dateTimeNowUtc, out DateTime fromUtc, out DateTime toUtc);
                if (!(dateTimeNowUtc >= fromUtc && dateTimeNowUtc <= toUtc))
                {
                    var task = redisTransaction.StringIncrementAsync($"{cacheKeyString}_OFP", allowedCallRate.Limit  + costPerCall);
                    redisTransaction.KeyExpireAsync($"{cacheKeyString}_OFP", new TimeSpan(0, 0, 10));
                    return task;
                }
            }
           
            var incrTask = redisTransaction.StringIncrementAsync(cacheKeyString, costPerCall);
            redisTransaction.KeyExpireAsync(cacheKeyString, cacheKey.Expiration);
            return incrTask;
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

