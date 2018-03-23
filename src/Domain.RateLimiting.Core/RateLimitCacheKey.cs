using System;
using System.Collections.Generic;

namespace Domain.RateLimiting.Core
{
    /// <summary>
    /// CacheKey used to keep track of the number of requested when rate limiting.
    /// </summary>
    public struct RateLimitCacheKey
    {
        private readonly Func<DateTime, string> _getSuffix;
        
        private readonly DateTime _dateTimeUtcNow;

        public string RequestId { get; }

        public string Method { get; }

        public string Host { get; }

        public string RouteTemplate { get; }

        private static readonly IDictionary<RateLimitUnit, Func<AllowedCallRate, TimeSpan>> RateLimitTypeExpirationMapping = 
            new Dictionary<RateLimitUnit, Func<AllowedCallRate, TimeSpan>>
        {
            {RateLimitUnit.PerSecond, limit => TimeSpan.FromSeconds(1)},
            {RateLimitUnit.PerMinute, limit => TimeSpan.FromMinutes(1)},
            {RateLimitUnit.PerHour, limit => TimeSpan.FromHours(1)},
            {RateLimitUnit.PerDay, limit => TimeSpan.FromDays(1)},
            {RateLimitUnit.PerCustomPeriod, limit => limit.Period.Duration}
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="RateLimitCacheKey" /> class.
        /// </summary>
        /// <param name="requestId">The request identifier.</param>
        /// <param name="method">The request method</param>
        /// <param name="host">The host</param>
        /// <param name="routeTemplate">The route template.</param>
        /// <param name="allowedCallRate">The rate limit entry.</param>
        /// <param name="getSuffix">The suffix function based on the process</param>
        /// <param name="clock"></param>
        /// <exception cref="System.ArgumentNullException">requestId</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">requestId;requestId cannot be empty</exception>
        /// <exception cref="ArgumentNullException">requestId or host or pathToLimit or expirationKey or httpMethod</exception>
        /// <exception cref="ArgumentOutOfRangeException">requestId;requestId cannot be empty or host;host cannot be empty or pathToLimit;requestId cannot be empty or  expirationKey;expirationKey cannot be empty or httpMethod;httpMethod cannot be empty</exception>
        public RateLimitCacheKey(string requestId, string method, string host, string routeTemplate, 
            AllowedCallRate allowedCallRate, Func<DateTime, string> getSuffix, IClock clock = null)
        {
            if (requestId == null) throw new ArgumentNullException(nameof(requestId));
            if (requestId.Length == 0) throw new ArgumentOutOfRangeException(nameof(requestId), "requestId cannot be empty");
            if (routeTemplate == null) throw new ArgumentNullException(nameof(routeTemplate));
            if (routeTemplate.Length == 0) throw new ArgumentOutOfRangeException(nameof(routeTemplate), "routeTemplate cannot be empty");

            _dateTimeUtcNow = clock?.GetUtcDateTime() ?? DateTime.UtcNow;

            RequestId = requestId;
            Method = method;
            Host = host;
            RouteTemplate = routeTemplate.StartsWith(@"/") ? routeTemplate : @"/" + routeTemplate;

            //////////////////////////////////////////////////////////////////////////////////
            Expiration = RateLimitTypeExpirationMapping[allowedCallRate.Unit](allowedCallRate);


            RetryAfter = _dateTimeUtcNow.Add(Expiration).ToString("R");
            AllowedCallRate = allowedCallRate;
            _getSuffix = getSuffix ?? (_ => string.Empty); 
        }

        /// <summary>
        /// The expiration timespan of the item with the cache key
        /// </summary>
        public readonly TimeSpan Expiration;

        /// <summary>
        /// The retry after response header value
        /// </summary>
        public readonly string RetryAfter;

        /// <summary>
        /// The unit associated with the rate limit value
        /// </summary>
        public RateLimitUnit Unit => AllowedCallRate.Unit;

        /// <summary>
        /// The request limit
        /// </summary>
        public int Limit => AllowedCallRate.Limit;

        /// <summary>
        /// The rate limiting policy for this key
        /// </summary>
        public readonly AllowedCallRate AllowedCallRate;
        
        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        public override string ToString()
        {
            return
                $"{RequestId}::{Method.ToUpperInvariant()} {Host.ToLowerInvariant()}{RouteTemplate.ToLowerInvariant()}::{_getSuffix(_dateTimeUtcNow)}";
        }
    }
}