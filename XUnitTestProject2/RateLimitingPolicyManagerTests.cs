using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace Domain.RateLimiting.Core.UnitTests
{
    public class RateLimitingPolicyManagerTests
    {
        [Fact]
        public async void ShouldApplyPolicyReturnedByCustomPolicyProviderWhenAllowedCallRatesIsNonEmptyAndSamePolicyHasBeenAddedStaticallyInManager()
        {
            await ArangeActAndAssert("/api/values", "GET", "testclient_01", "CustomProviderPolicy",
                new List<AllowedCallRate>()
                {
                    new AllowedCallRate(5, RateLimitUnit.PerMinute)
                });
        }

        public async void ShouldReturnNullPolicyWhenNullIsReturnedByCustomPolicyProviderEvenThoughSamePolicyHasBeenAddedStaticallyInManager()
        {
            await ArangeActAndAssert("/api/values", "GET", "testclient_01", "CustomProviderPolicy",
                returnNullPolicy:true);
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyForMatchingRequestKeyRouteMethodFromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            await ArangeActAndAssert("/api/values", "GET", "testclient_01", "RequestKey_Route_Method_MatchingPolicy_FromManager");
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyForRequestKeyRouteAllMethodsFromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            await ArangeActAndAssert("/api/values/{id}", "GET", "testclient_01", "RequestKey_Route_AllMethods_MatchingPolicy_FromManager");
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyForRequestKeyAllRoutesMethodFromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            await ArangeActAndAssert("/api/values/test", "GET", "testclient_01", "RequestKey_AllRoutes_Method_MatchingPolicy_FromManager");
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyForRequestKeyAllRoutesAllMethodsFromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            await ArangeActAndAssert("/api/items", "POST", "testclient_01", "RequestKey_AllRoutes_AllMethods_MatchingPolicy_FromManager");
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyForAllRequestKeysRouteMethodFromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            await ArangeActAndAssert("/api/values", "GET", "testclient_02", "AllRequestKeys_Route_Method_MatchingPolicy_FromManager");
        }


        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyForAllRequestKeysRouteAllMethodsFromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            await ArangeActAndAssert("/api/values", "POST", "testclient_02", "AllRequestKeys_Route_AllMethods_MatchingPolicy_FromManager");
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyForAllRequestKeysAllRoutesMethodFromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            await ArangeActAndAssert("/api/values/{id}", "GET", "testclient_02", "AllRequestKeys_AllRoutes_Method_MatchingPolicy_FromManager");
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyForAllRequestKeysAllRoutesAllMethodsFromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            await ArangeActAndAssert("/api/values/{id}", "HEAD", "testclient_02", "AllRequestKeys_AllRoutes_AllMethods_MatchingPolicy_FromManager");
        }

        private static void SetupPolicyManager(RateLimitingPolicyManager policyManager, 
            RateLimitingRequest rateLimtingRequest)
        {
            var allowedCallRates = new List<AllowedCallRate>()
            {
                new AllowedCallRate(5, RateLimitUnit.PerMinute)
            };

            policyManager.AddEndpointPolicy(new RateLimitPolicy("testclient_01", "/api/values",
                "GET", allowedCallRates, name: "RequestKey_Route_Method_MatchingPolicy_FromManager"));

            policyManager.AddEndpointPolicy(new RateLimitPolicy("testclient_01", "/api/values/{id}",
                "*", allowedCallRates, name: "RequestKey_Route_AllMethods_MatchingPolicy_FromManager"));
            
            policyManager.AddEndpointPolicy(new RateLimitPolicy("testclient_01", "*",
                "GET", allowedCallRates, name: "RequestKey_AllRoutes_Method_MatchingPolicy_FromManager"));

            policyManager.AddEndpointPolicy(new RateLimitPolicy("testclient_01", "*",
                "*", allowedCallRates, name: "RequestKey_AllRoutes_AllMethods_MatchingPolicy_FromManager"));

            policyManager.AddEndpointPolicy(new RateLimitPolicy("*", "/api/values",
                "GET", allowedCallRates, name: "AllRequestKeys_Route_Method_MatchingPolicy_FromManager"));

            policyManager.AddEndpointPolicy(new RateLimitPolicy("*", "/api/values",
                "*", allowedCallRates, name: "AllRequestKeys_Route_AllMethods_MatchingPolicy_FromManager"));

            policyManager.AddEndpointPolicy(new RateLimitPolicy("*", "*",
                "GET", allowedCallRates, name: "AllRequestKeys_AllRoutes_Method_MatchingPolicy_FromManager"));

            policyManager.AddEndpointPolicy(new RateLimitPolicy("*", "*",
                "*", allowedCallRates, name: "AllRequestKeys_AllRoutes_AllMethods_MatchingPolicy_FromManager"));
        }

        private static async Task ArangeActAndAssert(string routeTemplate, string method, string requestKey,
            string expectedPolicyNameToApply, IList<AllowedCallRate> allowedCallRates = null, bool allowAttributeOverride=false,
            bool returnNullPolicy = false)
        {
            var rateLimtingRequest = new RateLimitingRequest(routeTemplate,
                routeTemplate, method, s => new string[] { "s_value" }, null, null);

            var policyProviderMock = new Mock<IRateLimitingPolicyProvider>();
            policyProviderMock.Setup(provider => provider.GetPolicyAsync(rateLimtingRequest))
                .ReturnsAsync(returnNullPolicy ? null :
                    new RateLimitPolicy(requestKey, allowedCallRates, allowAttributeOverride, 
                    name:"CustomProviderPolicy"));

            var policyManager = new RateLimitingPolicyManager(policyProviderMock.Object);

            SetupPolicyManager(policyManager, rateLimtingRequest);

            var policyToApply = await policyManager.GetPolicyAsync(rateLimtingRequest);

            policyProviderMock.VerifyAll();

            if(returnNullPolicy)
                Assert.Null(policyToApply);
            else
                Assert.Equal(expectedPolicyNameToApply, policyToApply.Name);
        }
    }

    
}
