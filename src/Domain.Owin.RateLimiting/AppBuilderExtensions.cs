using Owin;
using System;
using Domain.RateLimiting.Core;

namespace Domain.Owin.RateLimiting
{
    /// <summary>
    /// OWIN application builder extensions
    /// </summary>
    public static class AppBuilderExtensions
    {
        /// <summary>
        /// Enables request rate limiting, using the specified policy
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="rateLimitingCacheProvider">The rateLimitingCacheProvider for example redis.</param>
        /// <param name="policyManager">The policy.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">app</exception>
        public static IAppBuilder UseRateLimiting(this IAppBuilder app, IRateLimitingCacheProvider rateLimitingCacheProvider,
            IRateLimitingPolicyParametersProvider policyManager)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            if (rateLimitingCacheProvider == null) throw new ArgumentNullException(nameof(rateLimitingCacheProvider));
            if (policyManager == null) throw new ArgumentNullException(nameof(policyManager));

            return app.Use(typeof(RequestLimitingMiddleware), rateLimitingCacheProvider, policyManager);
        }
    }
}
