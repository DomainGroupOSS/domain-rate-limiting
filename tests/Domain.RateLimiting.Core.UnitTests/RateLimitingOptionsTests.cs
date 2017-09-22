using System.Collections.Generic;
using System.Linq;
using Domain.RateLimiting.Core.Configuration;
using Xunit;

namespace Domain.RateLimiting.Core.UnitTests
{
    public class RateLimitingOptionsTests
    {
        [Fact]
        public void ShouldParsePoliciesFromOptionsCorrectly()
        {
            var rateLimitingOptions = new RateLimitingOptions()
            {
                RateLimitPolicyStrings = new List<string>()
                {
                    "*:GET:api/globallylimited/{id}:5_PerMinute&8_PerHour:true:StaticPolicy_0",
                    "*:*:/api/globallylimited/{id}/sub/{subid}:2_PerMinute:true:StaticPolicy_1",
                    "*:*:*:100_PerMinute:true:StaticPolicy_2"
                },
                RateLimitPolicyOptions = new List<RateLimitingPolicyOptions>()
                {
                    new RateLimitingPolicyOptions()
                    {
                        RouteTemplate = "api/globallylimited",
                        AllowAttributeOverride = true,
                        AllowedCallRates = new Dictionary<string, int>()
                        {
                            {RateLimitUnit.PerMinute.ToString(), 3} 
                        },
                        HttpMethod = "POST",
                        Name = "GlobalPostRate"
                    }
                }

            };

            var policies = rateLimitingOptions.RateLimitPolicies;

            var rateLimitPolicies = policies as RateLimitPolicy[] ?? policies.ToArray();

            Assert.Equal(4, rateLimitPolicies.Count());

            var policy0 = new RateLimitPolicy("*", "api/globallylimited/{id}", "GET", new List<AllowedCallRate>()
            {
                new AllowedCallRate(5, RateLimitUnit.PerMinute),
                new AllowedCallRate(8, RateLimitUnit.PerHour)
            }, true, "StaticPolicy_0");

            var policy1 = new RateLimitPolicy("*", "/api/globallylimited/{id}/sub/{subid}", "*", new List<AllowedCallRate>()
            {
                new AllowedCallRate(2, RateLimitUnit.PerMinute)
            }, true, "StaticPolicy_1");

            var policy2 = new RateLimitPolicy("*", "*", "*", new List<AllowedCallRate>()
            {
                new AllowedCallRate(100, RateLimitUnit.PerMinute)
            }, true, "StaticPolicy_2");


            var policy3 = new RateLimitPolicy("*", "api/globallylimited", "POST", new List<AllowedCallRate>()
            {
                new AllowedCallRate(3, RateLimitUnit.PerMinute)
            }, true, "GlobalPostRate");

            Assert.Equal(true, ContainsPolicy(policy0, rateLimitPolicies));

        }

        private static bool ContainsPolicy(RateLimitPolicy expectedPolicy, IEnumerable<RateLimitPolicy> policies)
        {
            return policies.Any(policy => 
                policy.Key.Equals(expectedPolicy.Key) && policy.AllowAttributeOverride == expectedPolicy.AllowAttributeOverride && 
                policy.Name == expectedPolicy.Name && policy.AllowedCallRates.All(expectedPolicy.AllowedCallRates.Contains));
        }

    }
}
