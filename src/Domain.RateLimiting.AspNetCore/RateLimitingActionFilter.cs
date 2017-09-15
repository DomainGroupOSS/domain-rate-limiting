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
                        actionContext.HttpContext.Request.Body)).ConfigureAwait(false);

            if (rateLimitingPolicy == null)
            {
                await base.OnActionExecutionAsync(actionContext, next);
                return;
            }

            var allowedCallRates = rateLimitingPolicy.AllowedCallRates;
            var routeTemplate = rateLimitingPolicy.RouteTemplate;
            var httpMethod = rateLimitingPolicy.HttpMethod;
            var name = rateLimitingPolicy.Name;

            if(rateLimitingPolicy.AllowAttributeOverride)
            {
                var attributeRates = GetCustomAttributes(actionContext.ActionDescriptor);
                if (attributeRates != null && attributeRates.Any())
                {
                    allowedCallRates = attributeRates;
                    routeTemplate = actionContext.ActionDescriptor.AttributeRouteInfo.Template;
                    httpMethod = actionContext.HttpContext.Request.Method;
                    name = $"AttributeOn_{routeTemplate}";
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
            
            var result = await _rateLimitingCacheProvider.LimitRequestAsync(requestKey, httpMethod,
                context.Request.Host.Value, routeTemplate, allowedCallRates).ConfigureAwait(false);

            if (result.Throttled)
                TooManyRequests(actionContext, result, name);
            else
            {
                AddUpdateRateLimitingSuccessHeaders(context, result);
                await base.OnActionExecutionAsync(actionContext, next);
            }
        }

        private static void AddUpdateRateLimitingSuccessHeaders(HttpContext context, RateLimitingResult result)
        {
            var successheaders = new Dictionary<string, string>()
            {
                {RateLimitHeaders.CallsRemaining, result.CallsRemaining.ToString()},
                {RateLimitHeaders.Limit, result.CacheKey.Limit.ToString() }
            };

            foreach (string successheader in successheaders.Keys)
            {
                if (context.Response.Headers.ContainsKey(successheader))
                {
                    context.Response.Headers[successheader] = new StringValues(
                        context.Response.Headers[successheader].ToArray()
                            .Append(successheaders[successheader]).ToArray());
                }
                else
                {
                    context.Response.Headers.Add(successheader, new StringValues(
                        new string[] { successheaders[successheader] }));
                }
            }
        }

        private static void InvalidRequestId(ActionExecutingContext context)
        {
            context.HttpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            context.HttpContext.Response.Headers.Clear();

            context.Result = new ContentResult()
            {
                Content = "An invalid request identifier was specified."
            };
        }

        private static void TooManyRequests(ActionExecutingContext context, 
            RateLimitingResult result, string violatedPolicyName = "")
        {
            var throttledResponseParameters =
                RateLimiter.GetThrottledResponseParameters(result, violatedPolicyName);
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
        
        private static IList<AllowedCallRate> GetCustomAttributes(ActionDescriptor actionDescriptor)
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