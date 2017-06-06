using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Domain.RateLimiting.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Domain.AspDotNetCore.RateLimiting
{
    /// <summary>
    ///     Action filter which rate limits requests using the action/controllers rate limit entry attribute.
    /// </summary>
    public class RateLimitingActionFilter : ActionFilterAttribute
    {
        private readonly IRateLimitingCacheProvider _rateLimitingCacheProvider;
        private readonly IRateLimitingPolicyParametersProvider _policyManager;
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
            _policyManager = policyManager ?? throw new ArgumentNullException(nameof(policyManager));
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="RateLimitingActionFilter" /> class.
        /// </summary>
        /// <param name="rateLimitingCacheProvider">The rate limiting cache provider.</param>
        /// <param name="policyManager"></param>
        /// <exception cref="System.ArgumentNullException">rateLimitingCacheProvider or rateLimitRequestKeyService</exception>
        public RateLimitingActionFilter(IRateLimitingCacheProvider rateLimitingCacheProvider,
            IRateLimitingPolicyParametersProvider policyManager) : 
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
            // ReSharper disable once PossibleNullReferenceException
            var rateLimitingPolicyParameters = await _policyManager?.GetPolicyParametersAsync(
                 new RateLimitingRequest(
                        actionContext.HttpContext.Request.Path,
                        actionContext.HttpContext.Request.Method,
                        () => actionContext.HttpContext.Request.Headers.ToDictionary((kv) => kv.Key, (kv) => kv.Value.ToArray()),
                        actionContext.HttpContext.User,
                        actionContext.HttpContext.Request.Body));

            if (rateLimitingPolicyParameters == null)
                return;

            var rateLimits = rateLimitingPolicyParameters.Policies;

            if (rateLimits == null || !rateLimits.Any())
                rateLimits = GetCustomAttributes(actionContext.ActionDescriptor);

            if (rateLimits == null || rateLimits.Count == 0)
                return;

            var context = actionContext.HttpContext;
            var requestKey = rateLimitingPolicyParameters.RequestKey;

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

            if (rateLimits.Any(
                rl => rl.WhiteListRequestKeys.Any(
                    k => string.Compare(requestKey, k, StringComparison.CurrentCultureIgnoreCase) == 0)))
            {
                return;
            }

            var result = await _rateLimitingCacheProvider.LimitRequestAsync(requestKey, context.Request.Method,
                context.Request.Host.Value, context.Request.Path.Value, rateLimits).ConfigureAwait(false);
            if (result.Throttled)
                TooManyRequests(actionContext, rateLimits, result.WaitingIntervalInTicks);
            else
                await base.OnActionExecutionAsync(actionContext, next);

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

        private void TooManyRequests(ActionExecutingContext context, IEnumerable<RateLimitPolicy> rateLimits, long waitingIntervalInTicks)
        {
            var rateLimitedResponseParameters =
                RateLimitingHelper.GetRateLimitedResponseParameters(waitingIntervalInTicks);
            context.HttpContext.Response.StatusCode = RateLimitedResponseParameters.StatusCode;
            context.HttpContext.Response.Headers.Add(rateLimitedResponseParameters.RetryAfterHeader, rateLimitedResponseParameters.RetryAfterInSecs);
            context.Result = new ContentResult()
            {
                Content = rateLimitedResponseParameters.Message
            };
        }
        
        private IList<RateLimitPolicy> GetCustomAttributes(ActionDescriptor actionDescriptor)
        {
            var controllerActionDescriptor = actionDescriptor as ControllerActionDescriptor;
            if (controllerActionDescriptor == null)
                return null;

            var policies = controllerActionDescriptor.MethodInfo.GetCustomAttributes<RateLimitPolicy>(true)?.ToList();
            if (policies == null || !policies.Any())
                policies = controllerActionDescriptor.ControllerTypeInfo.
                    GetCustomAttributes<RateLimitPolicy>(true)?.ToList();

            return policies;
        }

    }
}