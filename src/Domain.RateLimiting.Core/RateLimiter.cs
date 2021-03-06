using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Domain.RateLimiting.Core
{
    public class RateLimiter : IRateLimiter
    {
        private readonly IRateLimitingCacheProvider _rateLimitingCacheProvider;
        private readonly IRateLimitingPolicyProvider _policyProvider;

        public RateLimiter(IRateLimitingCacheProvider rateLimitingCacheProvider,
            IRateLimitingPolicyProvider policyProvider)
        {
            _rateLimitingCacheProvider = rateLimitingCacheProvider ?? throw new ArgumentNullException(nameof(rateLimitingCacheProvider));
            _policyProvider = policyProvider;
        }
        public async Task LimitRequestAsync( 
            RateLimitingRequest rateLimitingRequest,
            Func<IList<AllowedConsumptionRate>> getCustomAttributes, string host,
            Func<RateLimitingRequest, RateLimitPolicy, RateLimitingResult, Task> onPostLimitFuncAsync = null,
            Func<RateLimitingRequest, Task<RateLimitPolicy>> getPolicyFuncAsync = null,
            bool revert = false)
        {
            if (_policyProvider == null && getPolicyFuncAsync == null)
                throw new ArgumentNullException("There are no valid policy providers");
            
            var getPolicyAsync = getPolicyFuncAsync ??  _policyProvider.GetPolicyAsync;

            var rateLimitingPolicy = await getPolicyAsync(rateLimitingRequest).ConfigureAwait(false);

            if (rateLimitingPolicy == null)
            {
                if(onPostLimitFuncAsync != null)
                    await onPostLimitFuncAsync.Invoke(rateLimitingRequest, rateLimitingPolicy, new RateLimitingResult(ResultState.NotApplicable)).ConfigureAwait(false);

                return;
            }

            var allowedCallRates = rateLimitingPolicy.AllowedCallRates;
            var routeTemplate = rateLimitingPolicy.RouteTemplate;
            var httpMethod = rateLimitingPolicy.HttpMethod;
            var policyName = rateLimitingPolicy.Name;

            if (rateLimitingPolicy.AllowAttributeOverride)
            {
                var attributeRates = getCustomAttributes?.Invoke();
                if (attributeRates != null && attributeRates.Any())
                {
                    allowedCallRates = attributeRates;
                    routeTemplate = rateLimitingRequest.RouteTemplate;
                    httpMethod = rateLimitingRequest.Method;
                    policyName = $"AttributeOn_{routeTemplate}";
                }
            }

            if (allowedCallRates == null || !Enumerable.Any<AllowedConsumptionRate>(allowedCallRates))
            {
                if(onPostLimitFuncAsync != null)
                    await onPostLimitFuncAsync.Invoke(rateLimitingRequest, rateLimitingPolicy, new RateLimitingResult(ResultState.NotApplicable)).ConfigureAwait(false);

                return;
            }
            
            var rateLimitingResult = await _rateLimitingCacheProvider.LimitRequestAsync(rateLimitingPolicy.RequestKey, httpMethod,
                host, routeTemplate, allowedCallRates, 
                revert ? -rateLimitingPolicy.CostPerCall : rateLimitingPolicy.CostPerCall).ConfigureAwait(false);

            if (onPostLimitFuncAsync != null)
                await onPostLimitFuncAsync.Invoke(rateLimitingRequest, rateLimitingPolicy, rateLimitingResult).ConfigureAwait(false);
        }


        public static ThrottledResponseParameters GetThrottledResponseParameters(
            RateLimitingResult result, string violatedPolicyName = "")
        {
            var retryAfter = new TimeSpan(result.WaitingIntervalInTicks).TotalSeconds.ToString(CultureInfo.InvariantCulture);
            return new ThrottledResponseParameters(
                $"Request limit was exceeded for {violatedPolicyName} policy " +
                $"for the {result.CacheKey.AllowedConsumptionRate} rate. " +
                $"Please retry after {retryAfter} seconds from now.", new Dictionary<string, string>()
                {
                    { RateLimitHeaders.RetryAfter, retryAfter },
                    { RateLimitHeaders.ViolatedPolicyName, violatedPolicyName },
                    { RateLimitHeaders.ViolatedCallRate, result.CacheKey.AllowedConsumptionRate.ToString()}
                });
        }
    }
}