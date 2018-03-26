using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace Domain.RateLimiting.Core.UnitTests
{
    public class RateLimiterTests
    {
        private readonly IList<AllowedCallRate> _allowedCallRates = new List<AllowedCallRate>()
        {
            new AllowedCallRate(5, RateLimitUnit.PerMinute)
        };
        
        private readonly RateLimitingRequest _rateLimitingRequest = new RateLimitingRequest("/api/values",
            "/api/values", "GET", s => new string[] { "s_value" }, null, null);
        

        private async Task<(bool OnSuccessFuncCalled, bool OnThrottledFuncCalled, bool OnNotApplicableFuncCalled)> LimitRequestAync(IRateLimiter rateLimiter)
        {
            var onSuccessFuncCalled = false;
            var onThrottledFuncCalled = false;
            var onNotApplicableFuncCalled = false;

            await rateLimiter.LimitRequestAsync(_rateLimitingRequest,
                () => _allowedCallRates,
                "localhost",
                (r,p,rr) =>
                {
                    onSuccessFuncCalled = true;
                    return Task.CompletedTask;
                },
                (r,p,rr) =>
                {
                    onThrottledFuncCalled = true;
                    return Task.CompletedTask;
                },
                () =>
                {
                    onNotApplicableFuncCalled = true;
                    return Task.CompletedTask;
                });

            return (onSuccessFuncCalled, onThrottledFuncCalled, onNotApplicableFuncCalled);
        }

        private static void AssertAndVerify(Mock policyProviderMock,
            Mock rateLimitingCacheProviderMock, (bool, bool, bool) excepted,
            (bool, bool, bool) actual)
        {
            policyProviderMock.VerifyAll();
            rateLimitingCacheProviderMock.VerifyAll();

            Assert.Equal(excepted, actual);
        }

        [Fact]
        public async void ShouldApplyRateLimtingWithPolicyReturnedByPolicyProviderAndNotThrottle()
        {
            
            var policyProviderMock = new Mock<IRateLimitingPolicyProvider>();
            var rateLimitPolicy = new RateLimitPolicy("testclient_01",
                "/api/values", "GET", _allowedCallRates);

            policyProviderMock.Setup(provider => provider.GetPolicyAsync(_rateLimitingRequest))
                .ReturnsAsync(rateLimitPolicy);

            var rateLimitingCacheProviderMock = new Mock<IRateLimitingCacheProvider>();
            rateLimitingCacheProviderMock.Setup(provider => provider.LimitRequestAsync(
                "testclient_01", "GET",
                "localhost", "/api/values", _allowedCallRates, 1))
                .ReturnsAsync(new RateLimitingResult(false, 0));
            
            var rateLimiter = new RateLimiter(rateLimitingCacheProviderMock.Object, 
                policyProviderMock.Object);

            var result = await LimitRequestAync(rateLimiter);

            AssertAndVerify(policyProviderMock, rateLimitingCacheProviderMock, (true, false, false), result);
        }

        [Fact]
        public async void ShouldNotApplyRateLimtingWhenNullPolicyIsReturnedByPolicyProvider()
        {

            var policyProviderMock = new Mock<IRateLimitingPolicyProvider>(MockBehavior.Strict);
           
            policyProviderMock.Setup(provider => provider.GetPolicyAsync(_rateLimitingRequest))
                .ReturnsAsync(()=> null);

            var rateLimitingCacheProviderMock = new Mock<IRateLimitingCacheProvider>(MockBehavior.Strict);

            var rateLimiter = new RateLimiter(rateLimitingCacheProviderMock.Object,
                policyProviderMock.Object);

            var result = await LimitRequestAync(rateLimiter);

            AssertAndVerify(policyProviderMock, rateLimitingCacheProviderMock, (false, false, true), result);
        }

        [Fact]
        public async void ShouldNotApplyRateLimtingWhenPolicyWithWithNoAllowedCallRatesAndAttributeOverrideNotAllowedIsReturnedByPolicyProvider()
        {

            var policyProviderMock = new Mock<IRateLimitingPolicyProvider>(MockBehavior.Strict);
           
            policyProviderMock.Setup(provider => provider.GetPolicyAsync(_rateLimitingRequest))
                .ReturnsAsync(new RateLimitPolicy("testclient_01"));

            var rateLimitingCacheProviderMock = new Mock<IRateLimitingCacheProvider>(MockBehavior.Strict);

            var rateLimiter = new RateLimiter(rateLimitingCacheProviderMock.Object,
                policyProviderMock.Object);

            var result = await LimitRequestAync(rateLimiter);

            AssertAndVerify(policyProviderMock, rateLimitingCacheProviderMock, (false, false, true), result);
        }

        [Fact]
        public async void ShouldApplyRateLimtingWithCustomAttributesAndNotThrottleWhenPolicyWithWithNoAllowedCallRatesButAttributeOverrideAllowedIsReturnedByPolicyProvider()
        {
            var policyProviderMock = new Mock<IRateLimitingPolicyProvider>(MockBehavior.Strict);
           
            policyProviderMock.Setup(provider => provider.GetPolicyAsync(_rateLimitingRequest))
                .ReturnsAsync(new RateLimitPolicy("testclient_01",allowAttributeOverride:true));

            var rateLimitingCacheProviderMock = new Mock<IRateLimitingCacheProvider>(MockBehavior.Strict);
            rateLimitingCacheProviderMock.Setup(provider => provider.LimitRequestAsync(
                    "testclient_01", "GET",
                    "localhost", "/api/values", _allowedCallRates, 1))
                .ReturnsAsync(new RateLimitingResult(false, 0));

            var rateLimiter = new RateLimiter(rateLimitingCacheProviderMock.Object,
                policyProviderMock.Object);

            var result = await LimitRequestAync(rateLimiter);

            AssertAndVerify(policyProviderMock, rateLimitingCacheProviderMock, (true, false, false), result);
        }

        [Fact]
        public async void ShouldApplyRateLimtingAndThrottleWithPolicyReturnedByPolicyProvider()
        {

            var policyProviderMock = new Mock<IRateLimitingPolicyProvider>();
            
            var rateLimitPolicy = new RateLimitPolicy("testclient_01",
                "/api/values", "GET", _allowedCallRates, name:"Policy_01");

            policyProviderMock.Setup(provider => provider.GetPolicyAsync(_rateLimitingRequest))
                .ReturnsAsync(rateLimitPolicy);

            var rateLimitingCacheProviderMock = new Mock<IRateLimitingCacheProvider>();
            var rateLimitingResult = new RateLimitingResult(true, 1000,
                default(RateLimitCacheKey), 0, "Policy_01");

            rateLimitingCacheProviderMock.Setup(provider => provider.LimitRequestAsync(
                    "testclient_01", "GET",
                    "localhost", "/api/values", _allowedCallRates, 1))
                .ReturnsAsync(rateLimitingResult);

            var rateLimiter = new RateLimiter(rateLimitingCacheProviderMock.Object,
                policyProviderMock.Object);

            var result = await LimitRequestAync(rateLimiter);

            AssertAndVerify(policyProviderMock, rateLimitingCacheProviderMock, (false, true, false), result);

            //Assert.Equal("Policy_01", violatedPolicyName);
        }
    }
}
