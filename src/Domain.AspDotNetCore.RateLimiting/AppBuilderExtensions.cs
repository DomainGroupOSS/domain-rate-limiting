using System;
using Domain.RateLimiting.Core;
using Microsoft.AspNetCore.Builder;

namespace Domain.AspDotNetCore.RateLimiting
{
    /// <summary>
    /// AspDotNetCore application builder extensions
    /// </summary>
    public static class AppBuilderExtensions
    {
        /// <summary>
        /// Enables request rate limiting, using the specified policy
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <param name="rateLimitingCacheProvider">The rateLimitingCacheProvider for example redis.</param>
        /// <param name="policyManager">The policy.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">app</exception>
        public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder,
            IRateLimitingCacheProvider rateLimitingCacheProvider, 
            IRateLimitingPolicyParametersProvider policyManager)
        {
            if (rateLimitingCacheProvider == null) throw new ArgumentNullException(nameof(rateLimitingCacheProvider));
            if (policyManager == null) throw new ArgumentNullException(nameof(policyManager));

            return builder.UseMiddleware<RequestLimitingMiddleware>(rateLimitingCacheProvider, policyManager);
        }
    }
}
