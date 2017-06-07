using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Domain.RateLimiting.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Routing;

namespace Domain.AspDotNetCore.RateLimiting
{
    /// <summary>
    /// OWIN Middleware for performing request limiting
    /// </summary>
    public class RequestLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IRateLimitingPolicyParametersProvider _policyManager;
        private readonly IRateLimitingCacheProvider _rateLimitingCacheProvider;


        /// <summary>
        /// Initializes a new instance of the <see cref="RequestLimitingMiddleware" /> class.
        /// </summary>
        /// <param name="next">The next.</param>
        /// <param name="rateLimitingCacheProvider">The redis.</param>
        /// <param name="policyManager">The policy.</param>
        /// <exception cref="ArgumentNullException">redis</exception>
        /// <exception cref="System.ArgumentNullException">policy</exception>
        public RequestLimitingMiddleware(RequestDelegate next, 
            IRateLimitingCacheProvider rateLimitingCacheProvider,
            IRateLimitingPolicyParametersProvider policyManager)
        {
            _next = next;
            _rateLimitingCacheProvider = rateLimitingCacheProvider ?? throw new ArgumentNullException(nameof(rateLimitingCacheProvider));
            _policyManager = policyManager ?? throw new ArgumentNullException(nameof(policyManager));
        }

        /// <summary>
        /// Injects rate limiting functionality into the OWIN pipeline
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns></returns>IRateLimitingCacheProvider
        public async Task Invoke(HttpContext context)
        {
            var policyForCurrentRequest = await _policyManager.GetPolicyParametersAsync(new RateLimitingRequest(
                context.Request.Path.Value, context.Request.Method, 
                () => context.Request.Headers.ToDictionary((kv) => kv.Key, (kv) => kv.Value.ToArray()), 
                context.User, context.Request.Body));

            if (policyForCurrentRequest == null)
            {
                await _next.Invoke(context).ConfigureAwait(false);
                return;
            }

            var requestLimitId = policyForCurrentRequest.RequestKey;

            if (string.IsNullOrWhiteSpace(requestLimitId))
            {
                await InvalidRequestId(context).ConfigureAwait(false);
                return;
            }

            var rateLimitingResult = await _rateLimitingCacheProvider.LimitRequestAsync(requestLimitId, policyForCurrentRequest.HttpMethod,
                    context.Request.Host.Value, policyForCurrentRequest.Path, policyForCurrentRequest.Policies)
                .ConfigureAwait(false);

            if (!rateLimitingResult.Throttled)
                await _next.Invoke(context).ConfigureAwait(false);
            else
                await TooManyRequests(context, policyForCurrentRequest.Policies, rateLimitingResult.WaitingIntervalInTicks).ConfigureAwait(false);
        }

        private async Task InvalidRequestId(HttpContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            await context.Response.WriteAsync("An invalid request identifier was specified.").ConfigureAwait(false);
        }

        private async Task TooManyRequests(HttpContext context, IEnumerable<RateLimitPolicy> rateLimits, long waitingIntervalInTicks)
        {
            var rateLimitedResponseParameters =
                RateLimitingHelper.GetRateLimitedResponseParameters(waitingIntervalInTicks);
            context.Response.StatusCode = RateLimitedResponseParameters.StatusCode;
            context.Response.Headers.Add(rateLimitedResponseParameters.RetryAfterHeader, rateLimitedResponseParameters.RetryAfterInSecs);
            await context.Response.WriteAsync(rateLimitedResponseParameters.Message).ConfigureAwait(false);
        }
    }
}