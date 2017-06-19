using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Domain.RateLimiting.Core;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace Domain.RateLimiting.AspDotNet
{
    /// <summary>
    ///     Action filter which rate limits requests using the action/controllers rate limit entry attribute.
    /// </summary>
    public class RateLimitingActionFilter : ActionFilterAttribute
    {
        private readonly IRateLimitingCacheProvider _rateLimitingCacheProvider;
        private readonly IRateLimitingPolicyProvider _globalRateLimitingPolicy;
        private readonly IEnumerable<string> _whitelistedRequestKeys;

        /// <summary>
        ///     Initializes a new instance of the <see cref="RateLimitingActionFilter" /> class.
        /// </summary>
        /// <param name="rateLimitingCacheProvider">The rate limiting cache provider.</param>
        /// <param name="whitelistedRequestKeys">The request keys request keys to ignore when rate limiting.</param>
        /// <param name="policyManager">The global policy when rate limiting.</param>
        /// <exception cref="System.ArgumentNullException">
        ///     rateLimitingCacheProvider or rateLimitRequestKeyService or
        ///     whitelistedRequestKeys
        /// </exception>
        public RateLimitingActionFilter(IRateLimitingCacheProvider rateLimitingCacheProvider,
            IRateLimitingPolicyProvider policyManager,
            IEnumerable<string> whitelistedRequestKeys)
        {
            _rateLimitingCacheProvider = rateLimitingCacheProvider ?? throw new ArgumentNullException(nameof(rateLimitingCacheProvider));
            _whitelistedRequestKeys = whitelistedRequestKeys ?? throw new ArgumentNullException(nameof(whitelistedRequestKeys));
            _globalRateLimitingPolicy = policyManager;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="RateLimitingActionFilter" /> class.
        /// </summary>
        /// <param name="rateLimitingCacheProvider">The rate limiting cache provider.</param>
        /// <param name="policyManager"></param>
        /// <exception cref="System.ArgumentNullException">
        ///     rateLimitingCacheProvider or rateLimitRequestKeyService or
        ///     whitelistedRequestKeys
        /// </exception>
        public RateLimitingActionFilter(IRateLimitingCacheProvider rateLimitingCacheProvider,
            IRateLimitingPolicyProvider policyManager) : this(rateLimitingCacheProvider,
            policyManager, Enumerable.Empty<string>())
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="actionContext"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task OnActionExecutingAsync(HttpActionContext actionContext, CancellationToken cancellationToken)
        {
            var contentStream = await actionContext.Request.Content.ReadAsStreamAsync();
            // ReSharper disable once PossibleNullReferenceException
            var rateLimitingPolicy = await _globalRateLimitingPolicy?.GetPolicyAsync(
                new RateLimitingRequest(
                    actionContext.RequestContext.RouteData.Route.RouteTemplate, 
                    actionContext.Request.RequestUri.PathAndQuery,
                    actionContext.Request.Method.Method,
                    (header) => actionContext.Request.Headers.GetValues(header)?.ToArray(),
                    actionContext.RequestContext.Principal as ClaimsPrincipal, 
                    contentStream));

            if (rateLimitingPolicy == null)
            {
                return;
            }

            var allowedCallRates = rateLimitingPolicy.AllowedCallRates;
            var routeTemplate = rateLimitingPolicy.RouteTemplate;
            var httpMethod = rateLimitingPolicy.HttpMethod;

            if (rateLimitingPolicy.AllowAttributeOverride)
            {
                var attributeRates = GetRateLimitAttributes(actionContext);
                if (attributeRates != null && attributeRates.Any())
                {
                    allowedCallRates = attributeRates;
                    routeTemplate = actionContext.RequestContext.RouteData.Route.RouteTemplate;
                    httpMethod = actionContext.Request.Method.Method;
                }
            }

            if (allowedCallRates == null || !allowedCallRates.Any())
                return;

            var requestKey = rateLimitingPolicy.RequestKey;
            
            if (string.IsNullOrWhiteSpace(rateLimitingPolicy.RequestKey))
            {
                actionContext.Response = new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    ReasonPhrase = "Invalid request key when rate limiting request",
                };
                return;
            }

            if (_whitelistedRequestKeys.Any(k => string.Compare(requestKey,
                                                     k, StringComparison.InvariantCultureIgnoreCase) == 0))
            {
                return;
            }

            if (allowedCallRates.Any(
                rl => rl.WhiteListRequestKeys.Any(
                    k => string.Compare(requestKey, k,
                             StringComparison.InvariantCultureIgnoreCase) == 0)))
            {
                return;
            }

            var rateLimitingResult = await _rateLimitingCacheProvider.LimitRequestAsync(
                requestKey,
                httpMethod,
                actionContext.Request.RequestUri.Host, 
                routeTemplate, allowedCallRates).ConfigureAwait(false);

            if (rateLimitingResult.Throttled)
            {
                TooManyRequests(actionContext, rateLimitingResult, rateLimitingPolicy.Name);
            }
        }

        private void TooManyRequests(HttpActionContext actionContext,
            RateLimitingResult result, string violatedPolicyName = "")
        {
            var throttledResponseParameters =
                RateLimitingHelper.GetThrottledResponseParameters(result, violatedPolicyName);

            actionContext.Response = new HttpResponseMessage((HttpStatusCode)ThrottledResponseParameters.StatusCode)
            {
                ReasonPhrase = throttledResponseParameters.Message
            };

            // KAZI Revisit since there is an exception while adding the header complaining of
            // invalid format
            foreach (var header in throttledResponseParameters.RateLimitHeaders.Keys)
            {
                actionContext.Response.Headers.Add(header,
                    throttledResponseParameters.RateLimitHeaders[header]);
            }
           
        }

        private IList<AllowedCallRate> GetRateLimitAttributes(HttpActionContext actionContext)
        {
            var rateLimits = actionContext.ActionDescriptor.GetCustomAttributes<AllowedCallRate>(true);
            if (rateLimits == null || !rateLimits.Any())
                rateLimits = actionContext.ActionDescriptor.ControllerDescriptor.
                    GetCustomAttributes<AllowedCallRate>(true);

            return rateLimits;
        }
    }
}