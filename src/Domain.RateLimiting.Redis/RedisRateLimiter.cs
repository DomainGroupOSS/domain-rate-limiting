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
        protected IConnectionMultiplexer _redisConnection;
        protected ConfigurationOptions _redisConfigurationOptions;

        private readonly ICircuitBreaker _circuitBreakerPolicy;
        private readonly Action<RateLimitingResult> _onThrottled;
        private readonly bool _countThrottledRequests;
        private readonly Func<Task<IConnectionMultiplexer>> _connectToRedisFunc;
        private readonly IClock _clock;

        protected RedisRateLimiter(string redisEndpoint,
            Action<Exception> onException = null,
            Action<RateLimitingResult> onThrottled = null,
            int connectionTimeoutInMilliseconds = 2000,
            int syncTimeoutInMilliseconds = 1000,
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
                SetupConnectionConfiguration(redisEndpoint, connectionTimeoutInMilliseconds, syncTimeoutInMilliseconds);

            //SetupCircuitBreaker(faultThreshholdPerWindowDuration, faultWindowDurationInMilliseconds, circuitOpenDurationInSecs, onException, onCircuitOpened, onCircuitClosed);
            ConnectToRedis(onException);
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

        private void ConnectToRedis(Action<Exception> onException)
        {
            _redisConnection = _connectToRedisFunc != null ? _connectToRedisFunc.Invoke().Result :
                               ConnectionMultiplexer.ConnectAsync(_redisConfigurationOptions).Result;

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
                IList<Func<long>> numberOfRequestsMadePerAllowedCallRateAsync = new List<Func<long>>();

                IList<RateLimitCacheKey> cacheKeys = new List<RateLimitCacheKey>();

                foreach (var allowedCallRate in allowedCallRates)
                {
                    if (allowedCallRate.Unit == RateLimitUnit.PerCustomPeriod)
                    {
                        var dateTimeNowUtc = new DateTime(utcNowTicks, DateTimeKind.Utc);
                        GetDateRange(allowedCallRate, dateTimeNowUtc, out DateTime fromUtc, out DateTime toUtc);
                        if (!(dateTimeNowUtc >= fromUtc && dateTimeNowUtc <= toUtc))
                        {
                            return new RateLimitingResult(ResultState.NotApplicable);
                        }
                    }

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

                    var callsRemaining = (cacheKey.AllowedConsumptionRate.MaxBurst != 0 ? cacheKey.AllowedConsumptionRate.MaxBurst : cacheKey.Limit) -
                            numberOfRequestsMadePerAllowedCallRateAsync[i]();

                    if (minCallsRemaining > callsRemaining)
                    {
                        minCallsRemaining = callsRemaining > 0 ? (int)callsRemaining : 0;
                        minCallsCacheKey = cacheKey;
                    }

                    if (callsRemaining < 0)
                        violatedCacheKeys.Add((long)allowedCallRates[i].Unit, cacheKey);
                }

                if (!violatedCacheKeys.Any() || costPerCall < 0)
                    return new RateLimitingResult(ResultState.Success, 0, minCallsCacheKey, minCallsRemaining);

                var postViolationTransaction = redisDb.CreateTransaction();

                if (!_countThrottledRequests)
                    UndoUnsuccessfulRequestCount(postViolationTransaction, cacheKeys, utcNowTicks, costPerCall);

                var violatedCacheKey = violatedCacheKeys.Last().Value;

                var getOldestRequestTimestampInTicksFunc =
                    GetOldestRequestTimestampInTicksFunc(postViolationTransaction,
                        violatedCacheKey, utcNowTicks);

                var throttleState = ResultState.Throttled;

                try
                {
                    await ExecuteTransactionAsync(postViolationTransaction).ConfigureAwait(false);
                }
                catch
                {
                    if(!_countThrottledRequests)
                        throttleState = ResultState.ThrottledButCompensationFailed;
                }

                var rateLimitingResult = new RateLimitingResult(throttleState,
                        GetWaitingIntervalInTicks(getOldestRequestTimestampInTicksFunc,
                        violatedCacheKey, utcNowTicks), violatedCacheKey, 0);

                _onThrottled?.Invoke(rateLimitingResult);

                return rateLimitingResult;

            }, new RateLimitingResult(ResultState.LimitApplicationFailed)).ConfigureAwait(false);
        }

        public Task<RateLimitingResult> LimitRequestAsync(RateLimitCacheKey cacheKey)
        {
            return LimitRequestAsync(cacheKey.RequestId, cacheKey.Method, cacheKey.Host, cacheKey.RouteTemplate,
                new List<AllowedConsumptionRate>() { new AllowedConsumptionRate(cacheKey.Limit, cacheKey.Unit) }, 1);
        }

        public async Task<RateLimitingResult> LimitRequestAsync(string requestId, string method, string host, string routeTemplate,
            IList<AllowedConsumptionRate> rateLimitPolicies)
        {
            return await LimitRequestAsync(requestId, method, host, routeTemplate, rateLimitPolicies, 1).ConfigureAwait(false);
        }

        private long GetWaitingIntervalInTicks(Func<long> getOldestRequestTimestampInTicksFunc,
            RateLimitCacheKey violatedCacheKey,
            long utcNowTicks)
        {
            return getOldestRequestTimestampInTicksFunc() +
                   GetTicksPerUnit(violatedCacheKey.AllowedConsumptionRate) - utcNowTicks;
        }

        private async Task ExecuteTransactionAsync(ITransaction redisTransaction)
        {
            var redisTransactionTask = redisTransaction.ExecuteAsync();
            var transactionTask = await Task.WhenAny(redisTransactionTask).ConfigureAwait(false);

            if (!redisTransactionTask.IsCompleted ||
                redisTransactionTask.IsFaulted ||
                !await redisTransactionTask.ConfigureAwait(false))
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

        protected static void GetDateRange(AllowedConsumptionRate allowedCallRate, DateTime dateTimeUtc, out DateTime fromUtc, out DateTime toUtc)
        {
            var periodUnits = allowedCallRate.Period.Repeating ?
                                    Math.Floor(dateTimeUtc.Subtract(allowedCallRate.Period.StartDateTimeUtc).TotalHours
                                    / allowedCallRate.Period.Duration.TotalHours) : 0;

            fromUtc = periodUnits > 0 ? allowedCallRate.Period.StartDateTimeUtc.Add(
                new TimeSpan(allowedCallRate.Period.Duration.Ticks * Convert.ToInt64(periodUnits))) : 
                allowedCallRate.Period.StartDateTimeUtc;

            toUtc = fromUtc.Add(allowedCallRate.Period.Duration);
        }

        /// <summary>
        /// Rate limits a request using to cache key provided
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <returns></returns>


        protected abstract Func<long> GetNumberOfRequestsAsync(
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

        protected abstract Func<long> GetOldestRequestTimestampInTicksFunc(
            ITransaction postViolationTransaction, RateLimitCacheKey cacheKey,
            long utcNowTicks);
    }
}
