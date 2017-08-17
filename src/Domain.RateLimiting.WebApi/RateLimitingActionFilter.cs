using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using System.Web.Http.Routing;
using Domain.RateLimiting.Core;

namespace Domain.RateLimiting.WebApi
{
    /// <summary>
    ///     Action filter which rate limits requests using the action/controllers rate limit entry attribute.
    /// </summary>
    public class RateLimitingActionFilter : ActionFilterAttribute
    {
        private readonly IRateLimitingCacheProvider _rateLimitingCacheProvider;
        private readonly IRateLimitingPolicyProvider _policyManager;
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
            _rateLimitingCacheProvider = rateLimitingCacheProvider ??
                                         throw new ArgumentNullException(nameof(rateLimitingCacheProvider));
            _whitelistedRequestKeys = whitelistedRequestKeys ??
                                      throw new ArgumentNullException(nameof(whitelistedRequestKeys));
            _policyManager = policyManager ??
                             throw new ArgumentNullException(nameof(policyManager));
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="RateLimitingActionFilter" /> class.
        /// </summary>
        /// <param name="rateLimitingCacheProvider">The rate limiting cache provider.</param>
        /// <param name="policyManager"></param>
        /// <exception cref="System.ArgumentNullException">rateLimitingCacheProvider or rateLimitRequestKeyService</exception>
        public RateLimitingActionFilter(IRateLimitingCacheProvider rateLimitingCacheProvider,
            IRateLimitingPolicyProvider policyManager) :
            this(rateLimitingCacheProvider, policyManager, Enumerable.Empty<string>())
        {
        }

        public override Task OnActionExecutedAsync(HttpActionExecutedContext actionExecutedContext, CancellationToken cancellationToken)
        {
            if (actionExecutedContext.Request.Properties.ContainsKey("RateLimitingResult"))
                AddUpdateRateLimitingSuccessHeaders(actionExecutedContext,
                    (RateLimitingResult)actionExecutedContext.Request.Properties["RateLimitingResult"]);

            return base.OnActionExecutedAsync(actionExecutedContext, cancellationToken);
        }

        /// <summary>
        ///     Occurs before the action method is invoked.
        /// </summary>
        /// <param name="actionContext"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        public override async Task OnActionExecutingAsync(HttpActionContext actionContext, CancellationToken cancellationToken)
        {
            var routeTemplate = GetRouteTemplate(actionContext);

            var rateLimitingPolicy = await _policyManager.GetPolicyAsync(
                new RateLimitingRequest(
                    routeTemplate,
                    actionContext.Request.RequestUri.AbsolutePath,
                    actionContext.Request.Method.Method,
                    (header) => actionContext.Request.Headers.GetValues(header).ToArray(),
                    actionContext.RequestContext.Principal as ClaimsPrincipal,
                    await actionContext.Request.Content.ReadAsStreamAsync().ConfigureAwait(false))).ConfigureAwait(false);

            if (rateLimitingPolicy == null)
            {
                await base.OnActionExecutingAsync(actionContext, cancellationToken);
                return;
            }

            var allowedCallRates = rateLimitingPolicy.AllowedCallRates;
            routeTemplate = rateLimitingPolicy.RouteTemplate;
            var httpMethod = rateLimitingPolicy.HttpMethod;
            var name = rateLimitingPolicy.Name;

            if (rateLimitingPolicy.AllowAttributeOverride)
            {
                var attributeRates = GetCustomAttributes(actionContext);
                if (attributeRates != null && attributeRates.Any())
                {
                    allowedCallRates = attributeRates;
                    routeTemplate = actionContext.RequestContext.RouteData.Route.RouteTemplate;
                    httpMethod = actionContext.Request.Method.Method;
                    name = $"AttributeOn_{routeTemplate}";
                }
            }

            if (allowedCallRates == null || !allowedCallRates.Any())
                return;

            var requestKey = rateLimitingPolicy.RequestKey;

            if (string.IsNullOrWhiteSpace(requestKey))
            {
                InvalidRequestId(actionContext);
                return;
            }

            if (_whitelistedRequestKeys != null &&
                _whitelistedRequestKeys.Any(k => string.Compare(requestKey, k, StringComparison.CurrentCultureIgnoreCase) == 0))
            {
                return;
            }

            if (allowedCallRates.Any(
                rl => rl.WhiteListRequestKeys.Any(
                    k => string.Compare(requestKey, k, StringComparison.CurrentCultureIgnoreCase) == 0)))
            {
                return;
            }

            var result = await _rateLimitingCacheProvider.LimitRequestAsync(requestKey, httpMethod,
                actionContext.Request.Headers.Host, routeTemplate, allowedCallRates).ConfigureAwait(false);

            if (result.Throttled)
                TooManyRequests(actionContext, result, name);
            else
            {
                actionContext.Request.Properties.Add("RateLimitingResult", result);
                await base.OnActionExecutingAsync(actionContext, cancellationToken);
            }
        }

        private static string GetRouteTemplate(HttpActionContext actionContext)
        {
            var controller = actionContext.RequestContext.RouteData.Values.ContainsKey("controller")
                ? actionContext.RequestContext.RouteData.Values["controller"].ToString()
                : string.Empty;

            var action = actionContext.RequestContext.RouteData.Values.ContainsKey("action")
                ? actionContext.RequestContext.RouteData.Values["action"].ToString()
                : string.Empty;

            var routeTemplate = actionContext.RequestContext.RouteData.Route.RouteTemplate
                .Replace("{controller}", controller).Replace("{action}", action);

            return routeTemplate;
        }

        private static void AddUpdateRateLimitingSuccessHeaders(HttpActionExecutedContext context, RateLimitingResult result)
        {
            var successheaders = new Dictionary<string, string>()
            {
                {RateLimitHeaders.CallsRemaining, result.CallsRemaining.ToString()},
                {RateLimitHeaders.Limit, result.CacheKey.Limit.ToString() }
            };

            var response = context.Response;
            foreach (var successheader in successheaders.Keys)
            {
                if (response.Headers.Contains(successheader))
                {
                    // KAZI revisit
                    var successHeaderValues = response.Headers.GetValues(successheader)?.ToList() ?? new List<string>();
                    successHeaderValues.Add(successheaders[successheader]);
                    context.Response.Headers.Remove(successheader);
                    response.Headers.Add(successheader, successHeaderValues);
                }
                else
                {
                    response.Headers.Add(successheader, new string[] { successheaders[successheader] });
                }
            }
        }



        private static void InvalidRequestId(HttpActionContext context)
        {
            var response = context.Response ?? context.Request.CreateResponse();
            response.StatusCode = HttpStatusCode.Forbidden;
            response.ReasonPhrase = "An invalid request identifier was specified.";
            context.Response = response;
        }

        private static void TooManyRequests(HttpActionContext context,
            RateLimitingResult result, string violatedPolicyName = "")
        {
            var response = context.Response ?? context.Request.CreateResponse();

            var throttledResponseParameters =
                RateLimitingHelper.GetThrottledResponseParameters(result, violatedPolicyName);

            response.StatusCode = (HttpStatusCode)ThrottledResponseParameters.StatusCode;

            foreach (var header in throttledResponseParameters.RateLimitHeaders.Keys)
            {
                response.Headers.TryAddWithoutValidation(header,
                    throttledResponseParameters.RateLimitHeaders[header]);
            }

            response.ReasonPhrase = throttledResponseParameters.Message;
            context.Response = response;
        }
        private static IList<AllowedCallRate> GetCustomAttributes(HttpActionContext actionContext)
        {
            var rateLimits = actionContext.ActionDescriptor.GetCustomAttributes<AllowedCallRate>(true).ToList();
            if (rateLimits == null || !rateLimits.Any())
                rateLimits = actionContext.ActionDescriptor.ControllerDescriptor.
                    GetCustomAttributes<AllowedCallRate>(true).ToList();

            return rateLimits;
        }

    }
}