using System.Collections.Generic;
using Moq;
using Xunit;

namespace Domain.RateLimiting.Core.UnitTests
{
    public class RateLimitingPolicyManagerTests
    {
        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyForMatchingRequestKeyRouteMethodFromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            var rateLimtingRequest = new RateLimitingRequest("/api/values",
                "/api/values", "GET", s => new string[] { "s_value" }, null, null);

            var policyProviderMock = new Mock<IRateLimitingPolicyProvider>();
            policyProviderMock.Setup(provider => provider.GetPolicyAsync(rateLimtingRequest))
                .ReturnsAsync(new RateLimitPolicy("testclient_01"));

            var policyManager = new RateLimitingPolicyManager(policyProviderMock.Object);
            
            SetupPolicyManager(policyManager, rateLimtingRequest);

            var policyToApply = await policyManager.GetPolicyAsync(rateLimtingRequest);

            policyProviderMock.VerifyAll();

            Assert.Equal("RequestKey_Route_Method_MatchingPolicy_FromManager", policyToApply.Name);
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyForRequestKeyRouteAllMethodsFromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            var rateLimtingRequest = new RateLimitingRequest("/api/values/{id}",
                "/api/values/1", "GET", s => new string[] { "s_value" }, null, null);

            var policyProviderMock = new Mock<IRateLimitingPolicyProvider>();
            policyProviderMock.Setup(provider => provider.GetPolicyAsync(rateLimtingRequest))
                .ReturnsAsync(new RateLimitPolicy("testclient_01"));

            var policyManager = new RateLimitingPolicyManager(policyProviderMock.Object);

            SetupPolicyManager(policyManager, rateLimtingRequest);

            var policyToApply = await policyManager.GetPolicyAsync(rateLimtingRequest);

            policyProviderMock.VerifyAll();

            Assert.Equal("RequestKey_Route_AllMethods_MatchingPolicy_FromManager", policyToApply.Name);
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyForRequestKeyAllRoutesMethodFromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            var rateLimtingRequest = new RateLimitingRequest("/api/test/{id}",
                "/api/test/1", "GET", s => new string[] { "s_value" }, null, null);

            var policyProviderMock = new Mock<IRateLimitingPolicyProvider>();
            policyProviderMock.Setup(provider => provider.GetPolicyAsync(rateLimtingRequest))
                .ReturnsAsync(new RateLimitPolicy("testclient_01"));

            var policyManager = new RateLimitingPolicyManager(policyProviderMock.Object);

            SetupPolicyManager(policyManager, rateLimtingRequest);

            var policyToApply = await policyManager.GetPolicyAsync(rateLimtingRequest);

            policyProviderMock.VerifyAll();

            Assert.Equal("RequestKey_AllRoutes_Method_MatchingPolicy_FromManager", policyToApply.Name);
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyForRequestKeyAllRoutesAllMethodsFromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            var rateLimtingRequest = new RateLimitingRequest("/api/items",
                "/api/items", "POST", s => new string[] { "s_value" }, null, null);

           
            var policyProviderMock = new Mock<IRateLimitingPolicyProvider>();
            policyProviderMock.Setup(provider => provider.GetPolicyAsync(rateLimtingRequest))
                .ReturnsAsync(new RateLimitPolicy("testclient_01"));

            var policyManager = new RateLimitingPolicyManager(policyProviderMock.Object);

            SetupPolicyManager(policyManager, rateLimtingRequest);

            var policyToApply = await policyManager.GetPolicyAsync(rateLimtingRequest);

            policyProviderMock.VerifyAll();

            Assert.Equal("RequestKey_AllRoutes_AllMethods_MatchingPolicy_FromManager", policyToApply.Name);
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyForAllRequestKeysRouteMethodFromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            var rateLimtingRequest = new RateLimitingRequest("/api/values",
                "/api/values", "GET", s => new string[] { "s_value" }, null, null);
            

            var policyProviderMock = new Mock<IRateLimitingPolicyProvider>();
            policyProviderMock.Setup(provider => provider.GetPolicyAsync(rateLimtingRequest))
                .ReturnsAsync(new RateLimitPolicy("testclient_02"));

            var policyManager = new RateLimitingPolicyManager(policyProviderMock.Object);

            SetupPolicyManager(policyManager, rateLimtingRequest);

            var policyToApply = await policyManager.GetPolicyAsync(rateLimtingRequest);

            policyProviderMock.VerifyAll();

            Assert.Equal("AllRequestKeys_Route_Method_MatchingPolicy_FromManager", policyToApply.Name);
        }


        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyForAllRequestKeysRouteAllMethodsFromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            var rateLimtingRequest = new RateLimitingRequest("/api/values",
                "/api/values", "POST", s => new string[] { "s_value" }, null, null);
            
            var policyProviderMock = new Mock<IRateLimitingPolicyProvider>();
            policyProviderMock.Setup(provider => provider.GetPolicyAsync(rateLimtingRequest))
                .ReturnsAsync(new RateLimitPolicy("testclient_02"));

            var policyManager = new RateLimitingPolicyManager(policyProviderMock.Object);

            SetupPolicyManager(policyManager, rateLimtingRequest);

            var policyToApply = await policyManager.GetPolicyAsync(rateLimtingRequest);

            policyProviderMock.VerifyAll();

            Assert.Equal("AllRequestKeys_Route_AllMethods_MatchingPolicy_FromManager", policyToApply.Name);
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyForAllRequestKeysAllRoutesMethodFromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            var rateLimtingRequest = new RateLimitingRequest("/api/values/{id}",
                "/api/values/1", "GET", s => new string[] { "s_value" }, null, null);
            
            var policyProviderMock = new Mock<IRateLimitingPolicyProvider>();
            policyProviderMock.Setup(provider => provider.GetPolicyAsync(rateLimtingRequest))
                .ReturnsAsync(new RateLimitPolicy("testclient_02"));

            var policyManager = new RateLimitingPolicyManager(policyProviderMock.Object);

            SetupPolicyManager(policyManager, rateLimtingRequest);

            var policyToApply = await policyManager.GetPolicyAsync(rateLimtingRequest);

            policyProviderMock.VerifyAll();

            Assert.Equal("AllRequestKeys_AllRoutes_Method_MatchingPolicy_FromManager", policyToApply.Name);
        }

        [Fact]
        public async void ShouldFallbackToStaticallyAddedPolicyForAllRequestKeysAllRoutesAllMethodsFromManagerWhenPolicyWithNoAllowedCallRatesIsReturnedByCustomPolicyProvider()
        {
            var rateLimtingRequest = new RateLimitingRequest("/api/values/{id}",
                "/api/values/1", "HEAD", s => new string[] { "s_value" }, null, null);

            var allowedCallRates = new List<AllowedCallRate>()
            {
                new AllowedCallRate(5, RateLimitUnit.PerMinute)
            };

            var policyProviderMock = new Mock<IRateLimitingPolicyProvider>();
            policyProviderMock.Setup(provider => provider.GetPolicyAsync(rateLimtingRequest))
                .ReturnsAsync(new RateLimitPolicy("testclient_02"));

            var policyManager = new RateLimitingPolicyManager(policyProviderMock.Object);

            SetupPolicyManager(policyManager, rateLimtingRequest);

            var policyToApply = await policyManager.GetPolicyAsync(rateLimtingRequest);

            policyProviderMock.VerifyAll();

            Assert.Equal("AllRequestKeys_AllRoutes_AllMethods_MatchingPolicy_FromManager", policyToApply.Name);
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
    }

    
}
