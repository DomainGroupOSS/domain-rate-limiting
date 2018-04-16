using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Claims;
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
    public class RateLimitingFilter : IAsyncAuthorizationFilter
    {
        private readonly IRateLimiter _rateLimiter;

        private Func<RateLimitingRequest, AuthorizationFilterContext, Task<RateLimitPolicy>> GetPolicyAsyncFunc { get; }

        public RateLimitingFilter(IRateLimiter rateLimiter,
             Func<RateLimitingRequest, AuthorizationFilterContext, Task<RateLimitPolicy>> getPolicyAsyncFunc = null)
        {
            _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
            GetPolicyAsyncFunc = getPolicyAsyncFunc;
        }

        private static void AddUpdateRateLimitingSuccessHeaders(HttpContext context, RateLimitingResult result)
        {
            var successheaders = new Dictionary<string, string>()
            {
                {RateLimitHeaders.TokensRemaining, result.TokensRemaining.ToString()},
                {RateLimitHeaders.Limit, result.CacheKey.allowedConsumptionRate.ToString() }
            };

            foreach (var successheader in successheaders.Keys)
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

        private static void TooManyRequests(AuthorizationFilterContext context, 
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
        
        private static IList<AllowedConsumptionRate> GetCustomAttributes(ActionDescriptor actionDescriptor)
        {
            var controllerActionDescriptor = actionDescriptor as ControllerActionDescriptor;
            if (controllerActionDescriptor == null)
                return null;

            var policies = controllerActionDescriptor.MethodInfo.GetCustomAttributes<AllowedConsumptionRate>(true)?.ToList();
            if (policies == null || !policies.Any())
                policies = controllerActionDescriptor.ControllerTypeInfo.
                    GetCustomAttributes<AllowedConsumptionRate>(true)?.ToList();

            return policies;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext actionContext)
        {
            var context = actionContext;
            await _rateLimiter.LimitRequestAsync(
                new RateLimitingRequest(
                    actionContext.ActionDescriptor.AttributeRouteInfo.Template,
                    actionContext.HttpContext.Request.Path,
                    actionContext.HttpContext.Request.Method,
                    (header) => context.HttpContext.Request.Headers[header],
                    actionContext.HttpContext.User,
                    actionContext.HttpContext.Request.Body),
                () => GetCustomAttributes(actionContext.ActionDescriptor),
                actionContext.HttpContext.Request.Host.Value,
                async (request, policy, rateLimitingResult) =>
                {
                    if (rateLimitingResult.State == ResultState.Success)
                    {
                        AddUpdateRateLimitingSuccessHeaders(actionContext.HttpContext, rateLimitingResult);
                        await Task.FromResult<object>(null);
                    }
                    else if(rateLimitingResult.State == ResultState.Throttled)
                    {
                        TooManyRequests(actionContext, rateLimitingResult, policy.Name);
                        await Task.FromResult<object>(null);
                    }
                },
                //async (request, policy, rateLimitingResult) =>
                //{
                //    TooManyRequests(actionContext, rateLimitingResult, policy.Name);
                //    await Task.FromResult<object>(null);
                //}, 
                //null,
                async (rlr) =>
                {
                    return await GetPolicyAsyncFunc(rlr, actionContext);
                }).ConfigureAwait(false);
        }
    }
}