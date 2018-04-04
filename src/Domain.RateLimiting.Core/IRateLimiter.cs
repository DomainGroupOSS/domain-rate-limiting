using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Domain.RateLimiting.Core
{
    public interface IRateLimiter
    {
        Task LimitRequestAsync(
            RateLimitingRequest rateLimitingRequest,
            Func<IList<AllowedConsumptionRate>> getCustomAttributes, string host,
            Func<RateLimitingRequest, RateLimitPolicy, RateLimitingResult, Task> onSuccessFunc,
            Func<RateLimitingRequest, RateLimitPolicy, RateLimitingResult, Task> onThrottledFunc,
            Func<RateLimitingRequest, Task> onNotApplicableFunc,
            Func<RateLimitingRequest, Task<RateLimitPolicy>> getPolicyAsyncFunc = null);
    }
}
