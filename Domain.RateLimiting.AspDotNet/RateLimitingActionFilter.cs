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
        private readonly IRateLimitingPolicyParametersProvider _globalRateLimitingPolicy;
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
            IRateLimitingPolicyParametersProvider policyManager,
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
            IRateLimitingPolicyParametersProvider policyManager) : this(rateLimitingCacheProvider,
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
            var rateLimitingPolicyParameters = await _globalRateLimitingPolicy?.GetPolicyParametersAsync(
                new RateLimitingRequest(
                    actionContext.RequestContext.RouteData.Route.RouteTemplate, 
                    actionContext.Request.RequestUri.PathAndQuery,
                    actionContext.Request.Method.Method,
                    () => actionContext.Request.Headers.ToDictionary((kv) => kv.Key, (kv) => kv.Value?.ToArray()),
                    actionContext.RequestContext.Principal as ClaimsPrincipal, 
                    contentStream));

            if (rateLimitingPolicyParameters == null)
                return;

            var rateLimits = rateLimitingPolicyParameters.Policies;

            if (rateLimits == null || !rateLimits.Any())
                rateLimits = GetRateLimitAttributes(actionContext);

            if (rateLimits == null || rateLimits.Count == 0)
                return;
            
            if (string.IsNullOrWhiteSpace(rateLimitingPolicyParameters.RequestKey))
            {
                actionContext.Response = new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    ReasonPhrase = "Invalid request key when rate limiting request",
                };
                return;
            }

            if (_whitelistedRequestKeys.Any(k => string.Compare(rateLimitingPolicyParameters.RequestKey,
                                                     k, StringComparison.InvariantCultureIgnoreCase) == 0))
            {
                return;
            }

            if (rateLimits.Any(
                rl => rl.WhiteListRequestKeys.Any(
                    k => string.Compare(rateLimitingPolicyParameters.RequestKey, k,
                             StringComparison.InvariantCultureIgnoreCase) == 0)))
            {
                return;
            }

            var rateLimitingResult = await _rateLimitingCacheProvider.LimitRequestAsync(
                rateLimitingPolicyParameters.RequestKey,
                rateLimitingPolicyParameters.HttpMethod,
                actionContext.Request.RequestUri.Host, 
                rateLimitingPolicyParameters.RouteTemplate, rateLimits).ConfigureAwait(false);

            if (rateLimitingResult.Throttled)
            {
                TooManyRequests(actionContext, rateLimitingResult.WaitingIntervalInTicks);
            }
        }

        private void TooManyRequests(HttpActionContext actionContext, long waitingIntervalInTicks)
        {
            var rateLimitedResponseParameters =
                RateLimitingHelper.GetRateLimitedResponseParameters(waitingIntervalInTicks);

            actionContext.Response = new HttpResponseMessage((HttpStatusCode)RateLimitedResponseParameters.StatusCode)
            {
                ReasonPhrase = rateLimitedResponseParameters.Message
            };

            // KAZI Revisit since there is an exception while adding the header complaining of
            // invalid format
            actionContext.Response.Headers.Add(rateLimitedResponseParameters.RetryAfterHeader,
                rateLimitedResponseParameters.RetryAfterInSecs);
        }

        private IList<RateLimitPolicy> GetRateLimitAttributes(HttpActionContext actionContext)
        {
            var rateLimits = actionContext.ActionDescriptor.GetCustomAttributes<RateLimitPolicy>(true);
            if (rateLimits == null || !rateLimits.Any())
                rateLimits = actionContext.ActionDescriptor.ControllerDescriptor.
                    GetCustomAttributes<RateLimitPolicy>(true);

            return rateLimits;
        }
    }
}