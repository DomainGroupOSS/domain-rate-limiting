using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Domain.RateLimiting.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;

namespace Domain.RateLimiting.AspNetCore
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

        /// <summary>
        ///     Occurs before the action method is invoked.
        /// </summary>
        /// <param name="actionContext"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        public override async Task OnActionExecutionAsync(ActionExecutingContext actionContext, ActionExecutionDelegate next)
        {
            var rateLimitingPolicy = await _policyManager.GetPolicyAsync(
                 new RateLimitingRequest(
                        actionContext.ActionDescriptor.AttributeRouteInfo.Template,
                        actionContext.HttpContext.Request.Path,
                        actionContext.HttpContext.Request.Method,
                        (header) => actionContext.HttpContext.Request.Headers[header],
                        actionContext.HttpContext.User,
                        actionContext.HttpContext.Request.Body));

            if (rateLimitingPolicy == null)
            {
                await base.OnActionExecutionAsync(actionContext, next);
                return;
            }

            var allowedCallRates = rateLimitingPolicy.AllowedCallRates;
            var routeTemplate = rateLimitingPolicy.RouteTemplate;
            var httpMethod = rateLimitingPolicy.HttpMethod;

            if(rateLimitingPolicy.AllowAttributeOverride)
            {
                var attributeRates = GetCustomAttributes(actionContext.ActionDescriptor);
                if (attributeRates != null && attributeRates.Any())
                {
                    allowedCallRates = attributeRates;
                    routeTemplate = actionContext.ActionDescriptor.AttributeRouteInfo.Template;
                    httpMethod = actionContext.HttpContext.Request.Method;
                }
            }

            if (allowedCallRates == null || !allowedCallRates.Any())
                return;

            var context = actionContext.HttpContext;
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
                context.Request.Host.Value, routeTemplate, allowedCallRates).ConfigureAwait(false);

            if (result.Throttled)
                TooManyRequests(actionContext, result, rateLimitingPolicy.Name);
            else
            {
                AddUpdateRateLimitingSuccessHeaders(context, result);
                await base.OnActionExecutionAsync(actionContext, next);
            }
        }

        private void AddUpdateRateLimitingSuccessHeaders(HttpContext context, RateLimitingResult result)
        {
            context.Response.Headers.Add(RateLimitHeaders.CallsRemaining, new StringValues(
                new string[] {result.CallsRemaining.ToString()}));
            context.Response.Headers.Add(RateLimitHeaders.Limit, new StringValues(
                new string[] {result.CacheKey.AllowedCallRate.ToString()}));
        }

        private void InvalidRequestId(ActionExecutingContext context)
        {
            context.HttpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            context.HttpContext.Response.Headers.Clear();

            context.Result = new ContentResult()
            {
                Content = "An invalid request identifier was specified."
            };
        }

        private void TooManyRequests(ActionExecutingContext context, 
            RateLimitingResult result, string violatedPolicyName = "")
        {
            var throttledResponseParameters =
                RateLimitingHelper.GetThrottledResponseParameters(result, violatedPolicyName);
            context.HttpContext.Response.StatusCode = ThrottledResponseParameters.StatusCode;

            foreach (var header in throttledResponseParameters.RateLimitHeaders.Keys)
            {
                context.HttpContext.Response.Headers.Add(header, 
                    throttledResponseParameters.RateLimitHeaders[header]);
            }
            

            context.Result = new ContentResult()
            {
                Content = throttledResponseParameters.Message
            };
        }
        
        private IList<AllowedCallRate> GetCustomAttributes(ActionDescriptor actionDescriptor)
        {
            var controllerActionDescriptor = actionDescriptor as ControllerActionDescriptor;
            if (controllerActionDescriptor == null)
                return null;

            var policies = controllerActionDescriptor.MethodInfo.GetCustomAttributes<AllowedCallRate>(true)?.ToList();
            if (policies == null || !policies.Any())
                policies = controllerActionDescriptor.ControllerTypeInfo.
                    GetCustomAttributes<AllowedCallRate>(true)?.ToList();

            return policies;
        }

    }
}