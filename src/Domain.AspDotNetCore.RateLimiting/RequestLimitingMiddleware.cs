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
        private readonly IRateLimitingPolicyProvider _policyManager;
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
            IRateLimitingPolicyProvider policyManager)
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
            var policyForCurrentRequest = await _policyManager.GetPolicyAsync(
                new RateLimitingRequest(
                    context.Request.Path.Value, context.Request.Path.Value, context.Request.Method, 
                    (header) => context.Request.Headers[header].ToArray(), 
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
                    context.Request.Host.Value, policyForCurrentRequest.RouteTemplate, policyForCurrentRequest.AllowedCallRates)
                .ConfigureAwait(false);

            if (!rateLimitingResult.Throttled)
                await _next.Invoke(context).ConfigureAwait(false);
            else
                await TooManyRequests(context, rateLimitingResult, policyForCurrentRequest.Name).ConfigureAwait(false);
        }

        private async Task InvalidRequestId(HttpContext context)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            await context.Response.WriteAsync("An invalid request identifier was specified.").ConfigureAwait(false);
        }

        private async Task TooManyRequests(HttpContext context, RateLimitingResult result, string violatedPolicyName = "")
        {
            var throttledResponseParameters =
                RateLimitingHelper.GetThrottledResponseParameters(result, violatedPolicyName);
            context.Response.StatusCode = ThrottledResponseParameters.StatusCode;

            foreach (var header in throttledResponseParameters.RateLimitHeaders.Keys)
            {
                context.Response.Headers.Add(header, throttledResponseParameters.RateLimitHeaders[header]);
            }

            await context.Response.WriteAsync(throttledResponseParameters.Message).ConfigureAwait(false);
        }
    }
}