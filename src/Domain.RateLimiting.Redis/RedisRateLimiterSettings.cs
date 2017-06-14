using System;
using System.Collections.Generic;
using System.Text;
using Domain.RateLimiting.Core;

namespace Domain.RateLimiting.Redis
{
    public class RedisRateLimiterSettings
    {
        public RedisRateLimiterSettings()
        {
        }

        /// <summary>
        ///     Gets the rate limit redis cache connection string.
        /// </summary>
        /// <value>
        ///     The rate limit redis cache connection string.
        /// </value>
        public string RateLimitRedisCacheConnectionString { get; set; }

        public int FaultThreshholdPerWindowDuration { get; set; }

        public int FaultWindowDurationInMilliseconds { get; set; }

        public int CircuitOpenIntervalInSecs { get; set; }

        public int ConnectionTimeout { get; set; }

        public int SyncTimeout { get; set; }
        
        public bool CountThrottledRequests { get; set; }

        public RateLimitingProcess RateLimitingProcess { get; set; }

        public IRateLimitingCacheProvider GetRedisRateLimitingCacheProvider(
            Action<Exception> onException = null,
            Action<RateLimitingResult> onThrottled = null, 
            Action onCircuitOpened = null,
            Action onCircuitClosed = null,
            Action<Exception> onCircuitException = null)
        {
            if (RateLimitingProcess == RateLimitingProcess.SlidingWindow)
            {
                return new RedisSlidingWindowRateLimiter(RateLimitRedisCacheConnectionString,
                    onException,
                    onThrottled,
                    circuitBreaker: new DefaultCircuitBreaker(FaultThreshholdPerWindowDuration,
                        FaultWindowDurationInMilliseconds,
                        CircuitOpenIntervalInSecs,
                        onCircuitOpened,
                        onCircuitClosed),
                    connectionTimeout: ConnectionTimeout,
                    syncTimeout: SyncTimeout,
                    countThrottledRequests: CountThrottledRequests);
            }

            return new RedisFixedWindowRateLimiter(RateLimitRedisCacheConnectionString,
                onException,
                onThrottled,
                circuitBreaker: new DefaultCircuitBreaker(FaultThreshholdPerWindowDuration,
                    FaultWindowDurationInMilliseconds,
                    CircuitOpenIntervalInSecs,
                    onCircuitOpened,
                    onCircuitClosed),
                connectionTimeout: ConnectionTimeout,
                syncTimeout: SyncTimeout,
                countThrottledRequests: CountThrottledRequests);
        }
    }

    public enum RateLimitingProcess
    {
        FixedWindow,
        SlidingWindow
    }
}
