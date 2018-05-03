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

        private readonly string _filterId = Guid.NewGuid().ToString();

        private Func<RateLimitingRequest, HttpActionContext, Task<RateLimitPolicy>> GetPolicyFuncAsync { get; }
        public bool SimulationMode { get; }
        public Func<RateLimitingRequest, RateLimitPolicy, RateLimitingResult, HttpActionContext, Task<Decision>> OnPostLimit { get; }
        public Func<RateLimitingRequest, RateLimitPolicy, RateLimitingResult, HttpActionContext, Task> OnPostLimitRevert { get; }
        public Func<RateLimitingRequest, RateLimitPolicy, RateLimitingResult, HttpActionExecutedContext, Task<Decision>> PostOperationDecisionFuncAsync { get; }

        public RateLimitingFilter(IRateLimiter rateLimiter, HttpFilterCollection httpFilters,
            Func<RateLimitingRequest, RateLimitPolicy, RateLimitingResult, HttpActionContext, Task<Decision>> onPostLimit = null,
            Func<RateLimitingRequest, RateLimitPolicy, RateLimitingResult, HttpActionContext, Task> onPostLimitRevert = null,
            Func<RateLimitingRequest, RateLimitPolicy, RateLimitingResult, HttpActionExecutedContext, Task<Decision>> postOperationDecisionFuncAsync = null,
            Func<RateLimitingRequest, HttpActionContext, Task<RateLimitPolicy>> getPolicyFuncAsync = null,
            bool simulationMode = false)
        {
            _rateLimiter = rateLimiter;
            httpFilters.Add(new RateLimitingPostActionFilter(_filterId));
            OnPostLimit = onPostLimit;
            OnPostLimitRevert = onPostLimitRevert;
            PostOperationDecisionFuncAsync = postOperationDecisionFuncAsync;
            GetPolicyFuncAsync = getPolicyFuncAsync;
            SimulationMode = simulationMode;
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

                onPostLimitFuncAsync: async (rateLimitingRequest, policy, rateLimitingResult) =>
                {
                    context.Request.Properties.Add($"RateLimitingResult_{_filterId}", rateLimitingResult);

                    var clientDecision = OnPostLimit != null ? await OnPostLimit.Invoke(rateLimitingRequest, policy, rateLimitingResult, actionContext).ConfigureAwait(false) : Decision.OK;
                    var reverted = await RevertIfRequired(rateLimitingResult, context, request, policy, clientDecision).ConfigureAwait(false);

                    if (reverted)
                    {
                        await base.OnAuthorizationAsync(context, cancellationToken).ConfigureAwait(false);
                        return;
                    }

                    if (rateLimitingResult.State == ResultState.Success)
                    {
                        if (PostOperationDecisionFuncAsync != null)
                        {
                            context.Request.Properties.Add($"PostActionFilterFuncAsync_{_filterId}",
                                new Func<HttpActionExecutedContext, Task>(async (httpActionExecutedContext) =>
                                {
                                    var decision = 
                                        await PostOperationDecisionFuncAsync.Invoke(rateLimitingRequest, policy, rateLimitingResult, httpActionExecutedContext).ConfigureAwait(false);

                                    await RevertIfRequired(rateLimitingResult, context, request, policy, decision).ConfigureAwait(false);
                                }));
                        }

                        await base.OnAuthorizationAsync(context, cancellationToken).ConfigureAwait(false);
                    }
                    else if ((rateLimitingResult.State == ResultState.Throttled || rateLimitingResult.State == ResultState.ThrottledButCompensationFailed) 
                        && !SimulationMode)
                    {
                        await TooManyRequests(context, rateLimitingResult, policy.Name).ConfigureAwait(false);
                    }
                },
                getPolicyFuncAsync: GetPolicyFuncAsync != null ? new Func<RateLimitingRequest, Task<RateLimitPolicy>>(async (rlr) =>
                {
                    return await GetPolicyFuncAsync.Invoke(rlr, actionContext).ConfigureAwait(false);
                }) : null
            ).ConfigureAwait(false);
        }

        private async Task<bool> RevertIfRequired(RateLimitingResult rateLimitingResult, HttpActionContext context,
            RateLimitingRequest request, RateLimitPolicy policy, Decision decision)
        {
            if (decision == Decision.REVERTSUCCESSCOST && rateLimitingResult.State == ResultState.Success)
            {
                await _rateLimiter.LimitRequestAsync(request,
                    () => RateLimitingFilter.GetCustomAttributes(context),
                    context.Request.Headers.Host,
                    getPolicyFuncAsync: _ => Task.FromResult(policy),
                    onPostLimitFuncAsync: async (rateLimitingRequest, postPolicy, rateLimitingRevertResult) =>
                    {
                        if (rateLimitingRevertResult.State == ResultState.Success)
                            context.Request.Properties[$"RateLimitingResult_{_filterId}"] = rateLimitingRevertResult;

                        if(OnPostLimitRevert != null)
                            await OnPostLimitRevert.Invoke(request, postPolicy, rateLimitingRevertResult, context).ConfigureAwait(false);
                    },
                    revert: true).ConfigureAwait(false);

                return await Task.FromResult(true);                    
            }

            return await Task.FromResult(false);
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
        private static IList<AllowedConsumptionRate> GetCustomAttributes(HttpActionContext actionContext)
        {
            var rateLimits = actionContext.ActionDescriptor.GetCustomAttributes<AllowedConsumptionRate>(true).ToList();
            if (rateLimits == null || !rateLimits.Any())
                rateLimits = actionContext.ActionDescriptor.ControllerDescriptor.
                    GetCustomAttributes<AllowedConsumptionRate>(true).ToList();

            return rateLimits;
        }

    }
}