using System;

namespace Domain.RateLimiting.Core
{
    /// <summary>
    /// The desired unit to rate limit
    /// </summary>
    public enum RateLimitUnit:long
    {
        /// <summary>
        /// Limit requests per second
        /// </summary>
        PerSecond = TimeSpan.TicksPerSecond,

        /// <summary>
        /// Limit requests per minute
        /// </summary>
        PerMinute = TimeSpan.TicksPerMinute,

        /// <summary>
        /// Limit requests per hour
        /// </summary>
        PerHour = TimeSpan.TicksPerHour,

        /// <summary>
        /// Limit requests per day
        /// </summary>
        PerDay = TimeSpan.TicksPerDay,
        
        PerCustomPeriod = 0
    }

    public enum ResultState
    {
        Success,
        Throttled,
        NotApplicable,
        Exception
    }
}