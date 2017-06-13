using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.RateLimiting.Core;
using Domain.RateLimiting.Redis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Domain.AspDotNetCore.RateLimiting;

namespace Domain.RateLimiting.Samples.AspNetCore
{
    public class TestRateLimitingPolicyParametersProvider : IRateLimitingPolicyProvider
    {
        public Task<RateLimitPolicy> GetPolicyAsync(RateLimitingRequest rateLimitingRequest)
        {
            return Task.FromResult(new RateLimitPolicy("test_client"));
        }
    }
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var rateLimitCacheProvider = new RedisSlidingWindowRateLimiter("localhost", circuitBreaker: new CircuitBreaker(3, 10000, 5));

            var rateLimitingPolicyParametersProvider = new TestRateLimitingPolicyParametersProvider();

            var globalRateLimitingPolicy = new RateLimitingPolicyManager(rateLimitingPolicyParametersProvider)
                .AddPathToWhiteList("/api/unlimited")
                .AddPoliciesForAllEndpoints(new List<AllowedCallRate>()
                {
                    new AllowedCallRate(1000, RateLimitUnit.PerMinute)
                })
                .AddEndpointPolicies("/api/globallylimited", "*", new List<AllowedCallRate>() {
                    new AllowedCallRate(10, RateLimitUnit.PerMinute)
                })
                .AddEndpointPolicies("/api/globallylimited/{id}", "*", new List<AllowedCallRate>() {
                    new AllowedCallRate(5, RateLimitUnit.PerMinute)
                })
                .AddEndpointPolicies("/api/globallylimited/{id}/sub/{subid}", "*", new List<AllowedCallRate>() {
                    new AllowedCallRate(2, RateLimitUnit.PerMinute)
                })
                .AddEndpointPolicies("/api/attributelimited", "*", new List<AllowedCallRate>() {
                    new AllowedCallRate(20, RateLimitUnit.PerMinute)
                });
            // Add framework services.
            services.AddMvc(options =>
            {
                options.Filters.Add(new RateLimitingActionFilter(rateLimitCacheProvider, globalRateLimitingPolicy));
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            //var rateLimitCacheProvider = new RedisSlidingWindowRateLimiter("localhost", circuitBreaker: new CircuitBreaker(3, 10000, 5));

            //var rateLimitingPolicyParametersProvider = new TestRateLimitingPolicyParametersProvider();

            //var globalRateLimitingPolicy = new RateLimitingPolicyManager(rateLimitingPolicyParametersProvider)
            //    .AddPathToWhiteList("/api/unlimited")
            //    .AddPoliciesForAllEndpoints(new List<RateLimitPolicy>()
            //    {
            //        new RateLimitPolicy(1000, RateLimitUnit.PerMinute)
            //    })
            //    .AddEndpointPolicies("/api/globallylimited", "*", new List<RateLimitPolicy>() {
            //        new RateLimitPolicy(10, RateLimitUnit.PerMinute)
            //    })
            //    .AddEndpointPolicies("/api/attributelimited", "*", new List<RateLimitPolicy>() {
            //        new RateLimitPolicy(20, RateLimitUnit.PerMinute)
            //    });

            //app.UseRateLimiting(rateLimitCacheProvider, globalRateLimitingPolicy);
            app.UseMvc();
        }
    }
}
