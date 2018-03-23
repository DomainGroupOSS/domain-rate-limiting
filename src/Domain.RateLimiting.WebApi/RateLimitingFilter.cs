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
    public class RateLimitingFilter : AuthorizationFilterAttribute
    {
        private readonly IRateLimiter _rateLimiter;

        public Func<RateLimitingRequest, RateLimitPolicy, RateLimitingResult, Task> OnSuccess { get; }
        public Func<RateLimitingRequest, RateLimitPolicy, RateLimitingResult, Task> OnThrottled { get; }

        public RateLimitingFilter(IRateLimiter rateLimiter, 
            Func<RateLimitingRequest, RateLimitPolicy, RateLimitingResult,Task> onSuccess = null,
            Func<RateLimitingRequest, RateLimitPolicy, RateLimitingResult,Task> onThrottled = null)
        { 
            _rateLimiter = rateLimiter;
            OnSuccess = onSuccess;
            OnThrottled = onThrottled;
        }

        public override async Task OnAuthorizationAsync(HttpActionContext actionContext, CancellationToken cancellationToken)
        {
            var context = actionContext;
            var request = new RateLimitingRequest(
                    GetRouteTemplate(context),
                    context.Request.RequestUri.AbsolutePath,
                    context.Request.Method.Method,
                    (header) => context.Request.Headers.GetValues(header).ToArray(),
                    context.RequestContext.Principal as ClaimsPrincipal,
                    await context.Request.Content.ReadAsStreamAsync().ConfigureAwait(false));

            await _rateLimiter.LimitRequestAsync(
                request,
                () => RateLimitingFilter.GetCustomAttributes(context),
                context.Request.Headers.Host,
                async (rateLimitingRequest, policy, rateLimitingResult) =>
                {
                    if (!context.Request.Properties.ContainsKey("RateLimitingResult"))
                    {
                        context.Request.Properties.Add("RateLimitingResult", rateLimitingResult);
                    }

                    ///////////////////
                    if (OnSuccess != null)
                        await OnSuccess.Invoke(rateLimitingRequest, policy, rateLimitingResult);

                    await base.OnAuthorizationAsync(context, cancellationToken);
                },
                async (rateLimitingRequest, policy, rateLimitingResult) =>
                {
                    //////////////////////
                    if (OnThrottled != null)
                        await OnThrottled.Invoke(rateLimitingRequest, policy, rateLimitingResult);

                    await RateLimitingFilter.TooManyRequests(context, rateLimitingResult, policy.Name);
                },
                null).ConfigureAwait(false);
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