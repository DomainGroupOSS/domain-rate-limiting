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
        public Task<RateLimitPolicy> GetPolicyAsync(RateLimitingRequest rateLimitingRequest)
        {
            return Task.FromResult(new RateLimitPolicy("test_client"));
        }
    }

    #endregion

    #region SampleRateLimitingUserPolicyProvider
    //public class SampleRateLimitingUserPolicyProvider : IRateLimitingPolicyProvider
    //{
    //    private readonly Random _random = new Random();
    //    public Task<RateLimitPolicy> GetPolicyAsync(RateLimitingRequest rateLimitingRequest)
    //    {
    //        string[] users = new[] {"test_user_0", "test_user_1"};
    //        return Task.FromResult(new RateLimitPolicy(users[_random.Next(2)]));
    //    }
    //}
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
                #region Adding the RateLimitingActionFilter
                options.Filters.Add(new RateLimitingActionFilter(rateLimitCacheProvider, globalRateLimitingClientPolicyManager));
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
