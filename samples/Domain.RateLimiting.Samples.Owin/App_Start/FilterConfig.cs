using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http.Filters;
using Domain.Logging.Shippers.Elasticsearch.Serilog;
using Domain.RateLimiting.Core;
using Domain.RateLimiting.Redis;
using Domain.RateLimiting.WebApi;
using Serilog;

namespace Domain.RateLimiting.Samples.Owin
{

    public class CallClassification
    {
        public static Dictionary<string, string> RouteTemplateToClassMap = new Dictionary<string, string>()
        {
            { "api/globallylimited/{id}", "A"},
            { "api/globallylimited/{id}/sub/{subid}" , "B" },
            { "api/globallylimited/{id}/sub/{subid}/test/{testid}", "C" },
            { "api/globallylimited/{id}/free", "F" }
        };

        public static Dictionary<string, int> CostPerClass = 
            new Dictionary<string, int>()
            {
                { "A", 1},
                { "B", 10 },
                { "C", 100 },
                { "F", 0 }
            };
    }

    public class ClientQuotaPolicyProvider : IRateLimitingPolicyProvider
    {
        public Task<RateLimitPolicy> GetPolicyAsync(RateLimitingRequest rateLimitingRequest)
        {

            var clientId = rateLimitingRequest.ClaimsPrincipal?.Claims.FirstOrDefault(c => c.Type == "client_id");

            //if (string.IsNullOrWhiteSpace(clientId?.Value)) return null;

            var operationClass = CallClassification.RouteTemplateToClassMap[rateLimitingRequest.RouteTemplate];

            var cost = CallClassification.CostPerClass[operationClass];

            return Task.FromResult(new RateLimitPolicy("Test_Client_01",
                new List<AllowedCallRate>()
                {
                    new AllowedCallRate(1000, RateLimitUnit.PerCustomPeriod, new LimitPeriod()
                    {
                        StartDate = new DateTime(2018,3,23,0,0,0),
                        Duration = new TimeSpan(1,0,0),
                        Rolling = true
                    }),
                    new AllowedCallRate(100, RateLimitUnit.PerMinute)
                }, name:"Quota_CustomPeriod") { CostPerCall = cost });
        }
    }

    public class FilterConfig
    {
        public static void ConfigureRateLimiting(HttpFilterCollection filters)
        {
            #region Setting up the Redis rate limiter
            var redisRateLimiterSettings = new RedisRateLimiterSettings();

            ConfigureRateLimitingSettings(redisRateLimiterSettings);

            var rateLimitCacheProvider = new RedisFixedWindowRateLimiter(
                redisRateLimiterSettings.RateLimitRedisCacheConnectionString,
                circuitBreaker: new DefaultCircuitBreaker(redisRateLimiterSettings.FaultThreshholdPerWindowDuration,
                    redisRateLimiterSettings.FaultWindowDurationInMilliseconds, redisRateLimiterSettings.CircuitOpenIntervalInSecs,
                    onCircuitOpened: () =>
                    {
                        //_logger.LogWarning("Rate limiting circuit opened")
                    },
                    onCircuitClosed: () =>
                    {
                        //logger.LogWarning("Rate limiting circuit closed")
                    })
                );

            #endregion

            var configSettings = new DomainElasticSearchLoggingOptions()
            {
                Enabled = true,
                Url = "http://localhost:9200",
                Application = "Public Adapter",
                IndexFormat = "quotaaudits-{0:yyyy.MM.dd}"
            };

            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext();

            // if enabled in appsettings.json, logs will be shipped to Elasticsearch in a standard json format
            if (configSettings.Enabled)
                loggerConfig.WriteTo.DomainElasticsearch(configSettings);

            var auditLogger = loggerConfig.CreateLogger();

            filters.Add(new RateLimitingFilter(
                new RateLimiter(rateLimitCacheProvider, new ClientQuotaPolicyProvider()),
                async (request, policy, result) =>
                {
                    var operationClass = CallClassification.RouteTemplateToClassMap[request.RouteTemplate];
                    var cost = CallClassification.CostPerClass[operationClass];

                    auditLogger.Information(
                        "Throttled {Throttled}: Request success for client {ClientId} and endpoint {Endpoint} with route {RouteTemplate} which is Class {Class} with Cost {Cost}",
                        false,
                        result.CacheKey.RequestId,
                        request.Path,
                        request.RouteTemplate,
                        operationClass,
                        cost);
                },
                async (request, policy, result) =>
                {
                    var operationClass = CallClassification.RouteTemplateToClassMap[request.RouteTemplate];
                    var cost = CallClassification.CostPerClass[operationClass];

                auditLogger.Information(
                    "Throttled {Throttled}: throttled for client {ClientId} and endpoint {Endpoint} with route {RouteTemplate} which is Class {Class} with Cost {Cost} by violating policy {ViolatedPolicy}",
                        true,
                        result.CacheKey.RequestId,
                        request.Path,
                        request.RouteTemplate,
                        operationClass,
                        cost,
                        $"{policy.Name}:{result.CacheKey.AllowedCallRate}");
                }));

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
