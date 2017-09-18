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
using Domain.RateLimiting.Core;

namespace Domain.RateLimiting.WebApi
{
    /// <summary>
    ///     Action filter which rate limits requests using the action/controllers rate limit entry attribute.
    /// </summary>
    public partial class RateLimitingActionFilter : AuthorizationFilterAttribute
    {
        private readonly IRateLimitingCacheProvider _rateLimitingCacheProvider;
        private readonly IRateLimitingPolicyProvider _policyManager;

        private readonly IRateLimiter _rateLimitingHelper;

        /// <summary>
        ///     Initializes a new instance of the <see cref="RateLimitingActionFilter" /> class.
        /// </summary>
        /// <param name="rateLimitingCacheProvider">The rate limiting cache provider.</param>
        /// <param name="policyManager">The global policy when rate limiting.</param>
        /// <exception cref="System.ArgumentNullException">
        ///     rateLimitingCacheProvider or rateLimitRequestKeyService or
        ///     whitelistedRequestKeys
        /// </exception>
        public RateLimitingActionFilter(IRateLimitingCacheProvider rateLimitingCacheProvider,
            IRateLimitingPolicyProvider policyManager)
        {
            _rateLimitingCacheProvider = rateLimitingCacheProvider ??
                                         throw new ArgumentNullException(nameof(rateLimitingCacheProvider));

            _policyManager = policyManager ??
                             throw new ArgumentNullException(nameof(policyManager));

            _rateLimitingHelper = new RateLimiter(_rateLimitingCacheProvider, _policyManager);
        }

        //public override Task OnActionExecutedAsync(HttpActionExecutedContext actionExecutedContext, CancellationToken cancellationToken)
        //{
        //    if (actionExecutedContext.Request.Properties.ContainsKey("RateLimitingResult"))
        //        AddUpdateRateLimitingSuccessHeaders(actionExecutedContext,
        //            (RateLimitingResult)actionExecutedContext.Request.Properties["RateLimitingResult"]);

        //    return base.OnActionExecutedAsync(actionExecutedContext, cancellationToken);
        //}


        public override async Task OnAuthorizationAsync(HttpActionContext actionContext, CancellationToken cancellationToken)
        {
            await _rateLimitingHelper.LimitRequestAsync(
                new RateLimitingRequest(
                    GetRouteTemplate(actionContext),
                    actionContext.Request.RequestUri.AbsolutePath,
                    actionContext.Request.Method.Method,
                    (header) => actionContext.Request.Headers.GetValues(header).ToArray(),
                    actionContext.RequestContext.Principal as ClaimsPrincipal,
                    await actionContext.Request.Content.ReadAsStreamAsync().ConfigureAwait(false)),
                () => GetCustomAttributes(actionContext),
                actionContext.Request.Headers.Host,
                async rateLimitingResult =>
                {
                    actionContext.Request.Properties.Add("RateLimitingResult", rateLimitingResult);
                    await base.OnAuthorizationAsync(actionContext, cancellationToken);
                },
                async (rateLimitingResult, violatedPolicyName) =>
                {
                    await TooManyRequests(actionContext, rateLimitingResult, violatedPolicyName);
                },
                null).ConfigureAwait(false);
        }

        /// <summary>
        ///     Occurs before the controller action method is invoked.
        /// </summary>
        /// <param name="actionContext"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        //public override async Task OnActionExecutingAsync(HttpActionContext actionContext, CancellationToken cancellationToken)
        //{
        //    var result = await _rateLimitingHelper.LimitRequestAsync(
        //        new RateLimitingRequest(
        //            GetRouteTemplate(actionContext),
        //            actionContext.Request.RequestUri.AbsolutePath,
        //            actionContext.Request.Method.Method,
        //            (header) => actionContext.Request.Headers.GetValues(header).ToArray(),
        //            actionContext.RequestContext.Principal as ClaimsPrincipal,
        //            await actionContext.Request.Content.ReadAsStreamAsync().ConfigureAwait(false)),
        //            () => GetCustomAttributes(actionContext), 
        //            actionContext.Request.Headers.Host, 
        //            async () => 
        //            {
        //                InvalidRequestId(actionContext);
        //                await Task.FromResult<object>(null);
        //            },
        //            async rateLimitingResult =>
        //            {
        //                actionContext.Request.Properties.Add("RateLimitingResult", rateLimitingResult);
        //                await base.OnActionExecutingAsync(actionContext, cancellationToken);
        //            },
        //            async (rateLimitingResult, violatedPolicyName) =>
        //            {
        //                await TooManyRequests(actionContext, rateLimitingResult, violatedPolicyName);
        //            }).ConfigureAwait(false);
        //}

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

        private static async Task TooManyRequests(HttpActionContext context,
            RateLimitingResult result, string violatedPolicyName = "")
        {
            var response = context.Response ?? context.Request.CreateResponse();

            var throttledResponseParameters =
                RateLimiter.GetThrottledResponseParameters(result, violatedPolicyName);

            response.StatusCode = (HttpStatusCode)ThrottledResponseParameters.StatusCode;

            foreach (var header in throttledResponseParameters.RateLimitHeaders.Keys)
            {
                response.Headers.TryAddWithoutValidation(header,
                    throttledResponseParameters.RateLimitHeaders[header]);
            }

            response.ReasonPhrase = throttledResponseParameters.Message;
            context.Response = response;

            await Task.FromResult<object>(null);
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