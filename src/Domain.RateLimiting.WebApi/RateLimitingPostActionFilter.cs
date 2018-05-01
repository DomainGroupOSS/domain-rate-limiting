using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using Domain.RateLimiting.Core;

namespace Domain.RateLimiting.WebApi
{
    internal class RateLimitingPostActionFilter : ActionFilterAttribute
    {
        private readonly string _filterId;

        public RateLimitingPostActionFilter(string filterId)
        {
            _filterId = filterId;
        }

        public override async Task OnActionExecutedAsync(HttpActionExecutedContext actionExecutedContext, CancellationToken cancellationToken)
        {
            if (actionExecutedContext.Request.Properties.ContainsKey($"PostActionFilterFuncAsync_{_filterId}"))
            {
                var func = actionExecutedContext.Request.Properties[$"PostActionFilterFuncAsync_{_filterId}"]
                    as Func<HttpActionExecutedContext, Task>;

                await func?.Invoke(actionExecutedContext);
            }

            if (actionExecutedContext.Request.Properties.ContainsKey($"RateLimitingResult_{_filterId}"))
                AddUpdateRateLimitingSuccessHeaders(actionExecutedContext,
                    (RateLimitingResult)actionExecutedContext.Request.Properties[$"RateLimitingResult_{_filterId}"]);

            await base.OnActionExecutedAsync(actionExecutedContext, cancellationToken);
        }

        private static void AddUpdateRateLimitingSuccessHeaders(HttpActionExecutedContext context, RateLimitingResult result)
        {
            if (result.State == ResultState.LimitApplicationFailed || result.State == ResultState.NotApplicable)
                return;

            var successheaders = new Dictionary<string, string>()
            {
                {RateLimitHeaders.TokensRemaining, result.TokensRemaining.ToString()},
                {RateLimitHeaders.Limit, result.CacheKey.AllowedConsumptionRate?.ToString() }
            };

            if (context.Response == null)
                context.Response = context.Request.CreateErrorResponse(HttpStatusCode.InternalServerError, context.Exception);

            foreach (var successheader in successheaders.Keys)
            {
                if (context.Response.Headers.Contains(successheader))
                {
                    // KAZI revisit
                    var successHeaderValues = context.Response.Headers.GetValues(successheader)?.ToList() ?? new List<string>();
                    successHeaderValues.Add(successheaders[successheader]);
                    context.Response.Headers.Remove(successheader);
                    context.Response.Headers.Add(successheader, successHeaderValues);
                }
                else
                {
                    context.Response.Headers.Add(successheader, new string[] { successheaders[successheader] });
                }
            }
        }
    }
}