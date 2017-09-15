using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Domain.RateLimiting.Core
{
    public interface IRateLimiter
    {
        
    }

    public class RateLimitingHelper
    {
        private readonly IRateLimitingCacheProvider _rateLimitingCacheProvider;
        private readonly IRateLimitingPolicyProvider _policyProvider;

        public RateLimitingHelper(IRateLimitingCacheProvider rateLimitingCacheProvider,
            IRateLimitingPolicyProvider policyProvider)
        {
            _rateLimitingCacheProvider = rateLimitingCacheProvider;
            _policyProvider = policyProvider;
        }
        public async Task<RateLimitingResult> LimitRequestAsync(RateLimitingRequest rateLimitingRequest,
            Func<IList<AllowedCallRate>> getCustomAttributes, string host, Action onInvalidRequestKey)
        {

            var rateLimitingPolicy = await _policyProvider.GetPolicyAsync(rateLimitingRequest).ConfigureAwait(false);

            if (rateLimitingPolicy == null)
            {
                return new RateLimitingResult(false, 0);
            }

            var allowedCallRates = rateLimitingPolicy.AllowedCallRates;
            var routeTemplate = rateLimitingPolicy.RouteTemplate;
            var httpMethod = rateLimitingPolicy.HttpMethod;
            var policyName = rateLimitingPolicy.Name;

            if (rateLimitingPolicy.AllowAttributeOverride)
            {
                var attributeRates = getCustomAttributes();
                if (attributeRates != null && attributeRates.Any())
                {
                    allowedCallRates = attributeRates;
                    routeTemplate = rateLimitingRequest.RouteTemplate;
                    httpMethod = rateLimitingRequest.Method;
                    policyName = $"AttributeOn_{routeTemplate}";
                }
            }

            if (allowedCallRates == null || !allowedCallRates.Any())
                return new RateLimitingResult(false, 0);

            if (string.IsNullOrWhiteSpace(rateLimitingPolicy.RequestKey))
            {
                onInvalidRequestKey?.Invoke();
                return new RateLimitingResult(false, 0);
            }

            return await _rateLimitingCacheProvider.LimitRequestAsync(rateLimitingPolicy.RequestKey, httpMethod,
                host, routeTemplate, allowedCallRates).ConfigureAwait(false);
        }


        public static ThrottledResponseParameters GetThrottledResponseParameters(
            RateLimitingResult result, string violatedPolicyName = "")
        {
            var retryAfter = new TimeSpan(result.WaitingIntervalInTicks).TotalSeconds.ToString(CultureInfo.InvariantCulture);
            return new ThrottledResponseParameters(
                $"Request limit was exceeded for {violatedPolicyName} policy " +
                $"for the {result.CacheKey.AllowedCallRate} rate. " +
                $"Please retry after {retryAfter} seconds from now.", new Dictionary<string, string>()
                {
                    { RateLimitHeaders.RetryAfter, retryAfter },
                    { RateLimitHeaders.ViolatedPolicyName, violatedPolicyName },
                    { RateLimitHeaders.ViolatedCallRate, result.CacheKey.AllowedCallRate.ToString()}
                });
        }
    }
}
