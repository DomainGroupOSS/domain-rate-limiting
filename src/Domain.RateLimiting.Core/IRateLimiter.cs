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
            Func<RateLimitingRequest, RateLimitPolicy, RateLimitingResult, Task> onPostLimitFuncAsync = null,
            //Func<RateLimitingRequest, RateLimitPolicy, RateLimitingResult, Task> onSuccessFunc = null,
            //Func<RateLimitingRequest, RateLimitPolicy, RateLimitingResult, Task> onThrottledFunc = null,
            //Func<RateLimitingRequest, Task> onNotApplicableFunc = null,
            Func<RateLimitingRequest, Task<RateLimitPolicy>> getPolicyAsyncFunc = null,
            bool revert = false);
    }
}
