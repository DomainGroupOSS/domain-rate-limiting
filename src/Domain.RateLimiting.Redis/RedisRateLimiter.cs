using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.RateLimiting.Core;
using StackExchange.Redis;

namespace Domain.RateLimiting.Redis
{
    public abstract class RedisRateLimiter : IRateLimitingCacheProvider
    {
        private IConnectionMultiplexer _redisConnection;
        private ConfigurationOptions _redisConfigurationOptions;

        private readonly ICircuitBreaker _circuitBreakerPolicy;
        private readonly Action<RateLimitingResult> _onThrottled;
        private readonly bool _countThrottledRequests;
        private readonly Func<Task<IConnectionMultiplexer>> _connectToRedisFunc;
        private readonly IClock _clock;

        protected RedisRateLimiter(string redisEndpoint,
            Action<Exception> onException = null,
            Action<RateLimitingResult> onThrottled = null,
            int connectionTimeout = 2000,
            int syncTimeout = 1000,
            bool countThrottledRequests = false,
            ICircuitBreaker circuitBreaker = null,
            IClock clock = null,
            Func<Task<IConnectionMultiplexer>> connectToRedisFunc = null)
        {
            if (redisEndpoint == null) throw new ArgumentNullException(nameof(redisEndpoint));

            _onThrottled = onThrottled;
            _countThrottledRequests = countThrottledRequests;
            _connectToRedisFunc = connectToRedisFunc;
            _clock = clock;
            _circuitBreakerPolicy = circuitBreaker ??
                new DefaultCircuitBreaker(3, 10000, 300);

            if(connectToRedisFunc == null)
                SetupConnectionConfiguration(redisEndpoint, connectionTimeout, syncTimeout);

            //SetupCircuitBreaker(faultThreshholdPerWindowDuration, faultWindowDurationInMilliseconds, circuitOpenDurationInSecs, onException, onCircuitOpened, onCircuitClosed);
            ConnectToRedis(onException).GetAwaiter();
        }

        private void SetupConnectionConfiguration(string redisEndpoint, int connectionTimeout, int syncTimeout)
        {
            _redisConfigurationOptions = new ConfigurationOptions();
            _redisConfigurationOptions.EndPoints.Add(redisEndpoint);
            _redisConfigurationOptions.ClientName = "RedisRateLimiter";
            _redisConfigurationOptions.ConnectTimeout = connectionTimeout;
            _redisConfigurationOptions.SyncTimeout = syncTimeout;
            _redisConfigurationOptions.AbortOnConnectFail = false;
        }

        private async Task ConnectToRedis(Action<Exception> onException)
        {
            _redisConnection = _connectToRedisFunc != null ? await _connectToRedisFunc.Invoke() :
                               await ConnectionMultiplexer.ConnectAsync(_redisConfigurationOptions);

            if (_redisConnection == null || !_redisConnection.IsConnected)
            {
                onException?.Invoke(new Exception("Could not connect to redis server"));
            }
        }
        
        public async Task<RateLimitingResult> LimitRequestAsync(string requestId, string method, string host,
            string routeTemplate,
            IList<AllowedConsumptionRate> allowedCallRates,
            int costPerCall = 1)
        {
            return await _circuitBreakerPolicy.ExecuteAsync(async () =>
            {
                if (!_redisConnection.IsConnected)
                    throw new Exception("Redis is not connected at the moment");

                var redisDb = _redisConnection.GetDatabase();
                var redisTransaction = redisDb.CreateTransaction();
                var utcNowTicks = _clock?.GetCurrentUtcTimeInTicks() ?? DateTime.UtcNow.Ticks;
                IList<Task<long>> numberOfRequestsMadePerAllowedCallRateAsync = new List<Task<long>>();

                IList<RateLimitCacheKey> cacheKeys = new List<RateLimitCacheKey>();

                foreach (var allowedCallRate in allowedCallRates)
                {
                    numberOfRequestsMadePerAllowedCallRateAsync.Add(
                        GetNumberOfRequestsAsync(requestId, method, host, routeTemplate, allowedCallRate, cacheKeys,
                            redisTransaction, utcNowTicks, costPerCall));
                }

                await ExecuteTransactionAsync(redisTransaction).ConfigureAwait(false);

                var violatedCacheKeys = new SortedList<long, RateLimitCacheKey>();

                var minCallsRemaining = int.MaxValue;
                var minCallsCacheKey = default(RateLimitCacheKey);
                for (var i = 0; i < allowedCallRates.Count; i++)
                {
                    var cacheKey = cacheKeys[i];

                    var callsRemaining = cacheKey.Limit -
                            await numberOfRequestsMadePerAllowedCallRateAsync[i].ConfigureAwait(false);

                    if (minCallsRemaining > callsRemaining)
                    {
                        minCallsRemaining = callsRemaining > 0 ? (int)callsRemaining : 0;
                        minCallsCacheKey = cacheKey;
                    }

                    if (callsRemaining < 0)
                        violatedCacheKeys.Add((long)allowedCallRates[i].Unit, cacheKey);
                }

                if (!violatedCacheKeys.Any())
                    return new RateLimitingResult(ResultState.Success, 0, minCallsCacheKey, minCallsRemaining);

                var postViolationTransaction = redisDb.CreateTransaction();

                if (!_countThrottledRequests)
                    UndoUnsuccessfulRequestCount(postViolationTransaction, cacheKeys, utcNowTicks, costPerCall);

                var violatedCacheKey = violatedCacheKeys.Last().Value;

                var setupGetOldestRequestTimestampInTicks =
                    SetupGetOldestRequestTimestampInTicks(postViolationTransaction,
                        violatedCacheKey, utcNowTicks);

                await ExecuteTransactionAsync(postViolationTransaction).ConfigureAwait(false);

                var rateLimitingResult = new RateLimitingResult(ResultState.Throttled,
                    await GetWaitingIntervalInTicks(setupGetOldestRequestTimestampInTicks,
                        violatedCacheKey, utcNowTicks), violatedCacheKey, 0);

                _onThrottled?.Invoke(rateLimitingResult);

                return rateLimitingResult;

            }, new RateLimitingResult(ResultState.Exception));
        }

        public Task<RateLimitingResult> LimitRequestAsync(RateLimitCacheKey cacheKey)
        {
            return LimitRequestAsync(cacheKey.RequestId, cacheKey.Method, cacheKey.Host, cacheKey.RouteTemplate,
                new List<AllowedConsumptionRate>() { new AllowedConsumptionRate(cacheKey.Limit, cacheKey.Unit) }, 1);
        }

        public async Task<RateLimitingResult> LimitRequestAsync(string requestId, string method, string host, string routeTemplate,
            IList<AllowedConsumptionRate> rateLimitPolicies)
        {
            return await LimitRequestAsync(requestId, method, host, routeTemplate, rateLimitPolicies, 1);
        }

        private async Task<long> GetWaitingIntervalInTicks(Task<SortedSetEntry[]> setupGetOldestRequestTimestampInTicks,
            RateLimitCacheKey violatedCacheKey,
            long utcNowTicks)
        {
            return await GetOldestRequestTimestampInTicks(setupGetOldestRequestTimestampInTicks,
                       violatedCacheKey, utcNowTicks).ConfigureAwait(false) +
                   GetTicksPerUnit(violatedCacheKey.AllowedCallRate) - utcNowTicks;
        }

        private async Task ExecuteTransactionAsync(ITransaction redisTransaction)
        {
            var redisTransactionTask = redisTransaction.ExecuteAsync();
            var transactionTask = await Task.WhenAny(redisTransactionTask).ConfigureAwait(false);

            if (!redisTransactionTask.IsCompleted ||
                redisTransactionTask.IsFaulted ||
                !await redisTransactionTask)
            {
                throw new Exception("Redis transaction did not succeed", redisTransactionTask.Exception);
            }
        }

        private void UndoUnsuccessfulRequestCount(ITransaction undoUnsuccessfulRequestCountTransaction,
            IEnumerable<RateLimitCacheKey> cacheKeys,
            long utcNowTicks, int costPerCall = 1)
        {
            foreach (var cacheKey in cacheKeys)
                UndoUnsuccessfulRequestCount(undoUnsuccessfulRequestCountTransaction, cacheKey, utcNowTicks, costPerCall);

        }

        protected long GetTicksPerUnit(AllowedConsumptionRate allowedCallRate)
        {
            return allowedCallRate.Unit != RateLimitUnit.PerCustomPeriod ?
                            (long)allowedCallRate.Unit : allowedCallRate.Period.Duration.Ticks;
        }

        /// <summary>
        /// Rate limits a request using to cache key provided
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <returns></returns>


        protected abstract Task<long> GetNumberOfRequestsAsync(
            string requestId,
            string method,
            string host,
            string routeTemplate,
            AllowedConsumptionRate policy,
            IList<RateLimitCacheKey> cacheKeys,
            ITransaction redisTransaction,
            long utcNowTicks,
            int costPerCall = 1);

        protected abstract void UndoUnsuccessfulRequestCount(
            ITransaction postViolationTransaction,
            RateLimitCacheKey cacheKey,
            long utcNowTicks, int costPerCall = 1);

        protected abstract Task<SortedSetEntry[]> SetupGetOldestRequestTimestampInTicks(
            ITransaction postViolationTransaction,
            RateLimitCacheKey cacheKey,
            long utcNowTicks);

        protected abstract Task<long> GetOldestRequestTimestampInTicks(
            Task<SortedSetEntry[]> task, RateLimitCacheKey cacheKey,
            long utcNowTicks);
    }
}
