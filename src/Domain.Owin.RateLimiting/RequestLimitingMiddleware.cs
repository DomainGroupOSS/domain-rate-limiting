using Microsoft.Owin;
using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Domain.RateLimiting.Core;

namespace Domain.Owin.RateLimiting
{
    /// <summary>
    /// OWIN Middleware for performing request limiting
    /// </summary>
    public class RequestLimitingMiddleware : OwinMiddleware
    {
        private readonly IRateLimitingPolicyProvider _policyManager;
        private readonly IRateLimitingCacheProvider _rateLimitingCacheProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestLimitingMiddleware"/> class.
        /// </summary>
        /// <param name="next"></param>
        public RequestLimitingMiddleware(OwinMiddleware next) : base(next) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestLimitingMiddleware" /> class.
        /// </summary>
        /// <param name="next">The next.</param>
        /// <param name="rateLimitingCacheProvider">The rate limiting cache provider like redis.</param>
        /// <param name="policyManager">The policy.</param>
        /// <exception cref="ArgumentNullException">redis</exception>
        /// <exception cref="System.ArgumentNullException">policy</exception>
        public RequestLimitingMiddleware(OwinMiddleware next, 
            IRateLimitingCacheProvider rateLimitingCacheProvider,
            IRateLimitingPolicyProvider policyManager)
            : base(next)
        {
            _rateLimitingCacheProvider = rateLimitingCacheProvider ?? throw new ArgumentNullException(nameof(rateLimitingCacheProvider));
            _policyManager = policyManager ?? throw new ArgumentNullException(nameof(policyManager));
        }

        /// <summary>
        /// Injects rate limiting functionality into the OWIN pipeline
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>IRateLimitingCacheProvider
        public override async Task Invoke(IOwinContext context)
        {
            var policyParametersForCurrentRequest = await _policyManager.GetPolicyAsync(
                new RateLimitingRequest(
                    context.Request.Path.Value, 
                    context.Request.Path.Value, 
                    context.Request.Method, 
                    () => context.Request.Headers.ToDictionary((kv)=>kv.Key, (kv)=>kv.Value?.ToArray()), 
                    context.Authentication.User, 
                    context.Request.Body));

            if (policyParametersForCurrentRequest == null)
            {
                await Next.Invoke(context);
                return;
            }
            
            if (string.IsNullOrWhiteSpace(policyParametersForCurrentRequest.RequestKey) ||
                policyParametersForCurrentRequest.RequestKey.Length == 0)
            {
                await InvalidRequestId(context);
                return;
            }

            var rateLimitingResult =
                await _rateLimitingCacheProvider.LimitRequestAsync(policyParametersForCurrentRequest.RequestKey, 
                    policyParametersForCurrentRequest.HttpMethod,
                    context.Request.Host.Value, policyParametersForCurrentRequest.RouteTemplate, 
                    policyParametersForCurrentRequest.AllowedCallRates)
                    .ConfigureAwait(false);

            if (!rateLimitingResult.Throttled)
                await Next.Invoke(context).ConfigureAwait(false);
            else
                await TooManyRequests(context, rateLimitingResult.WaitingIntervalInTicks).ConfigureAwait(false);
        }

        private async Task InvalidRequestId(IOwinContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            await context.Response.WriteAsync("An invalid request identifier was specified.").ConfigureAwait(false);
        }

        private async Task TooManyRequests(IOwinContext context, long waitingIntervalInTicks)
        {
            var rateLimitedResponseParameters =
                RateLimitingHelper.GetRateLimitedResponseParameters(waitingIntervalInTicks);
            context.Response.StatusCode = RateLimitedResponseParameters.StatusCode;
            context.Response.Headers.Add(rateLimitedResponseParameters.RetryAfterHeader, 
                new string[] { rateLimitedResponseParameters.RetryAfterInSecs });
            await context.Response.WriteAsync(rateLimitedResponseParameters.Message).ConfigureAwait(false);
        }
    }
}