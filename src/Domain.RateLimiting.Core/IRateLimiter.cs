using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Domain.RateLimiting.Core
{
    public interface IRateLimiter
    {
        Task LimitRequestAsync(RateLimitingRequest rateLimitingRequest,
            Func<IList<AllowedCallRate>> getCustomAttributes, string host,
            Func<RateLimitingResult, Task> onSuccessFunc,
            Func<RateLimitingResult, string, Task> onThrottledFunc,
            Func<Task> onNotApplicableFunc);
    }
}
