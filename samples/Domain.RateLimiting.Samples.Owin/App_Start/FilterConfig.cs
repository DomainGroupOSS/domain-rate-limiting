using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
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
            { "api/unlimited/{id}", "F" }
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
        public Task<RateLimitPolicy> GetPolicyAsync(RateLimitingRequest rateLimitingRequest, HttpActionContext actionContext)
        {

            var clientId = rateLimitingRequest.ClaimsPrincipal?.Claims.FirstOrDefault(c => c.Type == "client_id");

            var operationClass = CallClassification.RouteTemplateToClassMap[rateLimitingRequest.RouteTemplate];

            if (actionContext != null)
            {
                var operationId = actionContext.ActionDescriptor.GetCustomAttributes<OperationInfo>(true).ToList();
                if (operationId.Count != 0)
                    operationClass = CallClassification.RouteTemplateToClassMap[operationId[0].Id];
            }

            //if (string.IsNullOrWhiteSpace(clientId?.Value)) return null;
            
            if (operationClass == "F")
            {
                return Task.FromResult(new RateLimitPolicy("Test_Client_01::ClassF",
                new List<AllowedConsumptionRate>()
                {
                    new AllowedConsumptionRate(100, RateLimitUnit.PerMinute)
                }, name: "QuotaFree_SafetyPolicy"));
            }

            var cost = CallClassification.CostPerClass[operationClass];

            return Task.FromResult(new RateLimitPolicy("Test_Client_01",
                new List<AllowedConsumptionRate>()
                {
                    //new AllowedConsumptionRate(200, RateLimitUnit.PerCustomPeriod,
                    //    new LimitPeriod(new DateTime(2018,3,23,0,0,0,DateTimeKind.Utc), 600, true)),
                    new AllowedConsumptionRate(200, RateLimitUnit.PerMinute)
                }, name: "Quota_Billed")
            { CostPerCall = cost });
        }

        public Task<RateLimitPolicy> GetPolicyAsync(RateLimitingRequest rateLimitingRequest)
        {
            return GetPolicyAsync(rateLimitingRequest, null);
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
                    }),
                countThrottledRequests:true
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

            var policyProvider = new ClientQuotaPolicyProvider();

            filters.Add(new RateLimitingFilter(

                new RateLimiter(rateLimitCacheProvider, policyProvider),

                onPostLimit: async (request, policy, result, actionContext) =>
                {
                    var operationClass = CallClassification.RouteTemplateToClassMap[request.RouteTemplate];
                    var cost = CallClassification.CostPerClass[operationClass];

                    var clientId = result.CacheKey.RequestId;
                    // sns publish

                    if (result.State == ResultState.Success)
                    {
                        // sns publish
                        auditLogger.Information(
                            "Result {Result}: Request success for client {ClientId} and endpoint {Endpoint} with route {RouteTemplate} which is Class {Class} with Cost {Cost}",
                            "Success",
                            clientId,
                            request.Path,
                            request.RouteTemplate,
                            operationClass,
                            cost);
                    }
                    else if (result.State == ResultState.Throttled)
                    {
                        auditLogger.Information(
                            "Result {Result}: throttled for client {ClientId} and endpoint {Endpoint} with route {RouteTemplate} which is Class {Class} with Cost {Cost} by violating policy {ViolatedPolicy}",
                            "Throttled",
                            clientId,
                            request.Path,
                            request.RouteTemplate,
                            operationClass,
                            cost,
                            $"{policy.Name}:{result.CacheKey.AllowedConsumptionRate}");
                    }
                    else if (result.State == ResultState.ThrottledButCompensationFailed)
                    {
                        auditLogger.Information(
                            "Result {Result}: throttled but failed to compensate for client {ClientId} and endpoint {Endpoint} with route {RouteTemplate} which is Class {Class} with Cost {Cost} by violating policy {ViolatedPolicy}",
                            result.State,
                            clientId,
                            request.Path,
                            request.RouteTemplate,
                            operationClass,
                            cost,
                            $"{policy.Name}:{result.CacheKey.AllowedConsumptionRate}");
                    }
                    else if (result.State == ResultState.LimitApplicationFailed)
                    {
                        auditLogger.Information(
                            "Result {Result}: Free pass for client {ClientId} and endpoint {Endpoint} with route {RouteTemplate} which is Class {Class} with Cost {Cost}",
                            "FreePass",
                            clientId,
                            request.Path,
                            request.RouteTemplate,
                            operationClass,
                            cost);
                    }
                    else if (result.State == ResultState.NotApplicable)
                    {
                        auditLogger.Information(
                           "Result {Result}: Not Applicable for client {ClientId} and endpoint {Endpoint} with route {RouteTemplate} which is Class {Class} with Cost {Cost}",
                           ResultState.NotApplicable,
                           clientId,
                           request.Path,
                           request.RouteTemplate,
                           operationClass,
                           cost);
                    }


                    return Decision.OK;
                },

                onPostLimitRevert: async (request, policy, result, actionContext) =>
                {
                    var operationClass = CallClassification.RouteTemplateToClassMap[request.RouteTemplate];

                    if (result.State == ResultState.Success || result.State == ResultState.Throttled)
                    {
                        auditLogger.Information(
                          "Result {Result}: Limit Cost Reverted for client {ClientId} and endpoint {Endpoint} with route {RouteTemplate} which is Class {Class} with Cost {Cost}",
                          "SuccessCostReverted",
                          result.CacheKey.RequestId,
                          request.Path,
                          request.RouteTemplate,
                          operationClass,
                          -policy.CostPerCall);
                    }
                    else if(result.State == ResultState.LimitApplicationFailed)
                    {
                        auditLogger.Information(
                          "Result {Result}: Limit Cost Reverting failed for client {ClientId} and endpoint {Endpoint} with route {RouteTemplate} which is Class {Class} with Cost {Cost}",
                          "SuccessCostRevertingFailed",
                          result.CacheKey.RequestId,
                          request.Path,
                          request.RouteTemplate,
                          operationClass,
                          -policy.CostPerCall);
                    }

                    return Decision.OK;
                },

                 postOperationDecisionFuncAsync: async (request, policy, result, actionExecutedContext) =>
                 {
                     if (actionExecutedContext.Exception != null || (int)actionExecutedContext.Response.StatusCode >= 400)
                         return Decision.REVERTSUCCESSCOST;

                     return Decision.OK;
                 },

                getPolicyFuncAsync: policyProvider.GetPolicyAsync,
                simulationMode: false));

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

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class OperationInfo : Attribute
    {
        public OperationInfo(string id)
        {
            Id = id;
        }

        public string Id { get; }
    }

}
