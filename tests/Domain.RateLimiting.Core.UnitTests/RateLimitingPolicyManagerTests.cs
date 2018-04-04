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
                new List<AllowedConsumptionRate>()
                {
                    new AllowedConsumptionRate(5, RateLimitUnit.PerMinute)
                });
        }

        [Fact]
        public async void ShouldReturnNullPolicyWhenNullIsReturnedByCustomPolicyProviderEvenThoughSamePolicyHasBeenAddedStaticallyInManager()
        {
            await ArangeActAndAssert("/api/values", "GET", "testclient_01", null,
                returnNullPolicy:true);
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyForMatching_RequestKey_Route_Method_FromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            await ArangeActAndAssert("/api/values", "GET", "testclient_01", "RequestKey_Route_Method_MatchingPolicy_FromManager");
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyFor_RequestKey_Route_AllMethods_FromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            await ArangeActAndAssert("/api/values/{id}", "GET", "testclient_01", "RequestKey_Route_AllMethods_MatchingPolicy_FromManager");
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyFor_RequestKey_AllRoutes_Method_FromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            await ArangeActAndAssert("/api/values/test", "GET", "testclient_01", "RequestKey_AllRoutes_Method_MatchingPolicy_FromManager");
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyFor_RequestKey_AllRoutes_AllMethods_FromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            await ArangeActAndAssert("/api/items", "POST", "testclient_01", "RequestKey_AllRoutes_AllMethods_MatchingPolicy_FromManager");
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyFor_AllRequestKeys_Route_Method_FromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            await ArangeActAndAssert("/api/values", "GET", "testclient_02", "AllRequestKeys_Route_Method_MatchingPolicy_FromManager");
        }


        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyFor_AllRequestKeys_Route_AllMethods_FromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            await ArangeActAndAssert("/api/values", "POST", "testclient_02", "AllRequestKeys_Route_AllMethods_MatchingPolicy_FromManager");
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyFor_AllRequestKeys_AllRoutes_Method_FromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            await ArangeActAndAssert("/api/values/{id}", "GET", "testclient_02", "AllRequestKeys_AllRoutes_Method_MatchingPolicy_FromManager");
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyFor_AllRequestKeys_AllRoutes_AllMethods_FromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            await ArangeActAndAssert("/api/values/{id}", "HEAD", "testclient_02", "AllRequestKeys_AllRoutes_AllMethods_MatchingPolicy_FromManager");
        }

        [Fact]
        public async void ShouldNotFallbackToStaticallyAddedPolicyFor_AllRequestKeys_AllRoutes_AllMethodsFromManagerWhenPathIsWhiteListed()
        {
            await ArangeActAndAssert("/api/values/{id}", "HEAD", "testclient_02", null, 
                whiteListedPaths: new List<string>(){ "/api/values/{id}" }, isWhiteListedPathTest:true);
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyForAllRequestKeysAllRoutesAllMethodsFromManagerWhenRequestKeyIsNotWhiteListed()
        {
            await ArangeActAndAssert("/api/values/{id}", "HEAD", "testclient_03", "AllRequestKeys_AllRoutes_AllMethods_MatchingPolicy_FromManager");
        }

        [Fact]
        public async void ShouldNotFallbackToStaticallyAddedPolicyForAllRequestKeysAllRoutesAllMethodsFromManagerWhenRequestKeyIsWhiteListed()
        {
            await ArangeActAndAssert("/api/values/{id}", "HEAD", "testclient_03", null,
                whiteListedRequestKeys: new List<string>() { "testclient_03" });
        }

        private static void SetupPolicyManager(RateLimitingPolicyManager policyManager, 
            RateLimitingRequest rateLimtingRequest, 
            IList<string> whiteListedRequestKeys = null, IList<string> whiteListedPaths = null)
        {
            var allowedCallRates = new List<AllowedConsumptionRate>()
            {
                new AllowedConsumptionRate(5, RateLimitUnit.PerMinute)
            };

            policyManager.AddRequestKeysToWhiteList(whiteListedRequestKeys ?? new List<string>());
            policyManager.AddPathsToWhiteList(whiteListedPaths ?? new List<string>());


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
            string expectedPolicyNameToApply, IList<AllowedConsumptionRate> allowedCallRates = null, bool allowAttributeOverride=false,
            bool returnNullPolicy = false, IList<string> whiteListedRequestKeys = null, IList<string> whiteListedPaths = null,
            bool isWhiteListedPathTest = false)
        {
            var rateLimtingRequest = new RateLimitingRequest(routeTemplate,
                routeTemplate, method, s => new string[] { "s_value" }, null, null);

            var policyProviderMock = new Mock<IRateLimitingPolicyProvider>();

            if (!isWhiteListedPathTest)
            {
                policyProviderMock.Setup(provider => provider.GetPolicyAsync(rateLimtingRequest))
                    .ReturnsAsync(returnNullPolicy
                        ? null
                        : new RateLimitPolicy(requestKey, allowedCallRates, allowAttributeOverride,
                            name: "CustomProviderPolicy"));
            }

            var policyManager = new RateLimitingPolicyManager(policyProviderMock.Object);

            SetupPolicyManager(policyManager, rateLimtingRequest, whiteListedRequestKeys, whiteListedPaths);

            var policyToApply = await policyManager.GetPolicyAsync(rateLimtingRequest);

            policyProviderMock.VerifyAll();

            if(expectedPolicyNameToApply == null)
                Assert.Null(policyToApply);
            else
                Assert.Equal(expectedPolicyNameToApply, policyToApply.Name);
        }
    }

    
}
