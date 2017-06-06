using System;
using System.Globalization;

namespace Domain.RateLimiting.Core
{
    public static class RateLimitingHelper
    {
        public static RateLimitedResponseParameters GetRateLimitedResponseParameters(long waitingIntervalInTicks)
        {
            var retryAfter = new TimeSpan(waitingIntervalInTicks).TotalSeconds.ToString(CultureInfo.InvariantCulture);
            return new RateLimitedResponseParameters(
                $"Request limit was exceeded. Please retry after {retryAfter} seconds from now.", "Retry-After", retryAfter);
        }
    }
}
