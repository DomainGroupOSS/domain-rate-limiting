using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Mvc;
using Domain.RateLimiting.Core;
using Domain.RateLimiting.Redis;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http.Filters;
using Domain.RateLimiting.WebApi;

namespace Domain.RateLimiting.Samples.WebApi
{

    public class SampleRateLimitingClientPolicyProvider : IRateLimitingPolicyProvider
    {
        public Task<RateLimitPolicy> GetPolicyAsync(RateLimitingRequest rateLimitingRequest)
        {
            return Task.FromResult(new RateLimitPolicy("test_client"));
        }
    }
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(HttpFilterCollection filters)
        {
            //filters.Add(new HandleErrorAttribute());

            ConfigureRateLimiting(filters);
        }

        public static void ConfigureRateLimiting(HttpFilterCollection filters)
        {
            var rateLimitingPolicyParametersProvider = new SampleRateLimitingClientPolicyProvider();
            var globalRateLimitingClientPolicyManager =
                new RateLimitingPolicyManager(rateLimitingPolicyParametersProvider)
                    .AddPathToWhiteList("/api/unlimited")
                    .AddPoliciesForAllEndpoints(new List<AllowedCallRate>()
                    {
                        new AllowedCallRate(100, RateLimitUnit.PerMinute)
                    }, allowAttributeOverride: true, name: "StaticPolicy_2")
                    .AddEndpointPolicy("/api/globallylimited/{id}", "GET", new List<AllowedCallRate>()
                    {
                        new AllowedCallRate(5, RateLimitUnit.PerMinute),
                        new AllowedCallRate(8, RateLimitUnit.PerHour)
                    }, true, "StaticPolicy_0")
                    .AddEndpointPolicy("/api/globallylimited/{id}/sub/{subid}", RateLimitPolicy.AllHttpMethods,
                        new List<AllowedCallRate>()
                        {
                            new AllowedCallRate(2, RateLimitUnit.PerMinute)
                        }, true, "StaticPolicy_1");

            #region Setting up the Redis rate limiter
            var redisRateLimiterSettings = new RedisRateLimiterSettings();

            ConfigureRateLimitingSettings(redisRateLimiterSettings);

            var rateLimitCacheProvider = new RedisSlidingWindowRateLimiter(
                redisRateLimiterSettings.RateLimitRedisCacheConnectionString,
                onThrottled: (rateLimitingResult) =>
                {
                    //_logger.LogInformation(
                    //    "Request throttled for client {ClientId} and endpoint {Endpoint}",
                    //    rateLimitingResult.CacheKey.RequestId,
                    //    rateLimitingResult.CacheKey.RouteTemplate);
                },
                circuitBreaker: new DefaultCircuitBreaker(redisRateLimiterSettings.FaultThreshholdPerWindowDuration,
                    redisRateLimiterSettings.FaultWindowDurationInMilliseconds, redisRateLimiterSettings.CircuitOpenIntervalInSecs,
                    onCircuitOpened: () =>
                    {
                        //_logger.LogWarning("Rate limiting circuit opened")
                    },
                    onCircuitClosed: () =>
                    {
                        //logger.LogWarning("Rate limiting circuit closed")
                    }));

            #endregion

            filters.Add(new RateLimitingFilter(new 
                RateLimiter(rateLimitCacheProvider, globalRateLimitingClientPolicyManager)));
            filters.Add(new RateLimitingPostActionFilter());
        }

        private static void ConfigureRateLimitingSettings(RedisRateLimiterSettings redisRateLimiterSettings)
        {
            redisRateLimiterSettings.RateLimitRedisCacheConnectionString =
                ConfigurationManager.AppSettings["RedisRateLimiterSettings:RateLimitRedisCacheConnectionString"];
            redisRateLimiterSettings.CircuitOpenIntervalInSecs =
                Int32.Parse(ConfigurationManager.AppSettings["RedisRateLimiterSettings:CircuitOpenIntervalInSecs"]);
            redisRateLimiterSettings.ConnectionTimeout =
                Int32.Parse(ConfigurationManager.AppSettings["RedisRateLimiterSettings:ConnectionTimeout"]);
            redisRateLimiterSettings.SyncTimeout =
                Int32.Parse(ConfigurationManager.AppSettings["RedisRateLimiterSettings:SyncTimeout"]);
            redisRateLimiterSettings.FaultThreshholdPerWindowDuration =
                Int32.Parse(ConfigurationManager.AppSettings["RedisRateLimiterSettings:FaultThreshholdPerWindowDuration"]);
            redisRateLimiterSettings.CountThrottledRequests =
                Boolean.Parse(ConfigurationManager.AppSettings["RedisRateLimiterSettings:CountThrottledRequests"]);
        }
    }
}
