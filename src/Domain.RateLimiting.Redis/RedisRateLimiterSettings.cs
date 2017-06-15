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
    }
}
