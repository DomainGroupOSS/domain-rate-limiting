using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.RateLimiting.AspNetCore;
using Domain.RateLimiting.Core;
using Domain.RateLimiting.Redis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Domain.RateLimiting.Samples.AspNetCore
{
    #region SampleRateLimitingClientPolicyProvider

    public class SampleRateLimitingClientPolicyProvider : IRateLimitingPolicyProvider
    {
        private readonly string _requestKey;

        public SampleRateLimitingClientPolicyProvider()
        {
            _requestKey = Guid.NewGuid().ToString();
        }
        public Task<RateLimitPolicy> GetPolicyAsync(RateLimitingRequest rateLimitingRequest)
        {
            return Task.FromResult(new RateLimitPolicy(_requestKey));
        }
    }

    #endregion

    #region Multi Level rate limiting on user and organization - Separate Policy Providers
    public class SampleRateLimitingUserPolicyProvider : IRateLimitingPolicyProvider
    {
        private static int _index = 1;
        public Task<RateLimitPolicy> GetPolicyAsync(RateLimitingRequest rateLimitingRequest)
        {
            var userId = "test_user_01";
            if (_index > 200 && _index <= 400)
                userId = "test_user_02";
            else if(_index > 400 && _index <= 600)
                userId = "test_user_03";
            
            _index++;

            if (_index > 600)
                _index = 1;

            return Task.FromResult(new RateLimitPolicy(userId, new List<AllowedConsumptionRate>()
            {
                new AllowedConsumptionRate(100, RateLimitUnit.PerHour)
            }));
        }
    }

    public class SampleRateLimitingOrganizationPolicyProvider : IRateLimitingPolicyProvider
    {
        public Task<RateLimitPolicy> GetPolicyAsync(RateLimitingRequest rateLimitingRequest)
        {
            return Task.FromResult(new RateLimitPolicy("test_oganization_01", new List<AllowedConsumptionRate>()
            {
                new AllowedConsumptionRate(200, RateLimitUnit.PerHour)
            }));
        }
    }
    #endregion

    public class Startup
    { 
        private readonly ILogger<Startup> _logger;
        public Startup(IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();

            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            _logger = loggerFactory.CreateLogger<Startup>();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Adds services required for using options.
            services.AddOptions();

            #region Setting policies explicitly using code

            var rateLimitingPolicyParametersProvider = new SampleRateLimitingClientPolicyProvider();
            var globalRateLimitingClientPolicyManager =
                new RateLimitingPolicyManager(rateLimitingPolicyParametersProvider)
                    .AddPathToWhiteList("/api/unlimited")
                    .AddPoliciesForAllEndpoints(new List<AllowedConsumptionRate>()
                    {
                        new AllowedConsumptionRate(1000, RateLimitUnit.PerMinute)
                    }, allowAttributeOverride: true, name: "StaticPolicy_2")
                    .AddEndpointPolicy("/api/globallylimited/{id}", "GET", new List<AllowedConsumptionRate>()
                    {
                        new AllowedConsumptionRate(5, RateLimitUnit.PerMinute),
                        new AllowedConsumptionRate(8, RateLimitUnit.PerHour)
                    }, true, "StaticPolicy_0")
                    .AddEndpointPolicy("/api/globallylimited/{id}/sub/{subid}", RateLimitPolicy.AllHttpMethods, 
                    new List<AllowedConsumptionRate>()
                    {
                        new AllowedConsumptionRate(2, RateLimitUnit.PerMinute)
                    }, true, "StaticPolicy_1");

            #endregion

            #region Setting Policies Using Configuration Options
            // var rateLimitingOptions = new RateLimitingOptions();
            // Configuration.GetSection(nameof(RateLimitingOptions)).Bind(rateLimitingOptions);

            // var globalRateLimitingClientPolicyManager = new RateLimitingPolicyManager(
            //        new SampleRateLimitingClientPolicyProvider())
            //    .AddPoliciesForAllEndpoints(new List<AllowedCallRate>() {new AllowedCallRate(180, RateLimitUnit.PerMinute)},name:"ClientPolicy")
            //    .AddPathsToWhiteList(rateLimitingOptions.RateLimitingWhiteListedPaths)
            //    .AddRequestKeysToWhiteList(rateLimitingOptions.RateLimitingWhiteListedRequestKeys);
            #endregion

            #region Setting up the Redis rate limiter
            var redisRateLimiterSettings = new RedisRateLimiterSettings();
            Configuration.GetSection(nameof(RedisRateLimiterSettings)).Bind(redisRateLimiterSettings);

            var rateLimitCacheProvider = new RedisSlidingWindowRateLimiter(
                redisRateLimiterSettings.RateLimitRedisCacheConnectionString,
                (exp) => _logger.LogError("Error in rate limiting",
                    exp),
                onThrottled: (rateLimitingResult) =>
                {
                    _logger.LogInformation(
                        "Request throttled for client {ClientId} and endpoint {Endpoint}",
                        rateLimitingResult.CacheKey.RequestId,
                        rateLimitingResult.CacheKey.RouteTemplate);
                },
                circuitBreaker: new DefaultCircuitBreaker(redisRateLimiterSettings.FaultThreshholdPerWindowDuration,
                    redisRateLimiterSettings.FaultWindowDurationInMilliseconds, redisRateLimiterSettings.CircuitOpenIntervalInSecs,
                    onCircuitOpened: () => _logger.LogWarning("Rate limiting circuit opened"),
                    onCircuitClosed: () => _logger.LogWarning("Rate limiting circuit closed")));

            #endregion


            // Add framework services
            services.AddMvc(options =>
            {
                #region Adding the RateLimitingFilter
                options.Filters.Add(new RateLimitingFilter(
                    new RateLimiter(rateLimitCacheProvider, globalRateLimitingClientPolicyManager)));
                #endregion

                #region Multi level rate limiting - Multiple action filters based on separate Policy Providers providing separate policies
                //options.Filters.Add(new RateLimitingFilter(
                //    new RateLimiter(rateLimitCacheProvider, new SampleRateLimitingUserPolicyProvider())));
                //options.Filters.Add(new RateLimitingFilter(
                //    new RateLimiter(rateLimitCacheProvider, new SampleRateLimitingOrganizationPolicyProvider())));
                #endregion  
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            app.UseMvc();
        }
    }
}
