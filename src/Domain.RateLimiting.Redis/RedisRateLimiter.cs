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
        private ConnectionMultiplexer _redisConnection;
        private ConfigurationOptions _redisConfigurationOptions;
        
        private readonly ICircuitBreaker _circuitBreakerPolicy;
        private readonly Action<RateLimitingResult> _onThrottled;
        private readonly bool _countThrottledRequests;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="redisEndpoint"></param>
        /// <param name="onException"></param>
        /// <param name="onThrottled"></param>
        /// <param name="connectionTimeout"></param>
        /// <param name="syncTimeout"></param>
        /// <param name="countThrottledRequests"></param>
        /// <param name="circuitBreaker"></param>
        protected RedisRateLimiter(string redisEndpoint,
            Action<Exception> onException = null,
            Action<RateLimitingResult> onThrottled = null,
            int connectionTimeout = 2000,
            int syncTimeout = 1000,
            bool countThrottledRequests = false,
            ICircuitBreaker circuitBreaker = null)
        {
            if (redisEndpoint == null) throw new ArgumentNullException(nameof(redisEndpoint));

            _onThrottled = onThrottled;
            _countThrottledRequests = countThrottledRequests;
            _circuitBreakerPolicy = circuitBreaker ?? 
                new DefaultCircuitBreaker(3, 10000, 300);
            SetupConnectionConfiguration(redisEndpoint, connectionTimeout, syncTimeout);
            //SetupCircuitBreaker(faultThreshholdPerWindowDuration, faultWindowDurationInMilliseconds, circuitOpenDurationInSecs, onException, onCircuitOpened, onCircuitClosed);
            ConnectToRedis(onException);
        }

        //private void SetupCircuitBreaker(int faultThreshholdPerWindowDuration,
        //    int faultWindowDurationInMilliseconds, int circuitOpenDurationInSecs, Action<Exception> onException,
        //    Action onCircuitOpened, Action onCircuitClosed)
        //{
        //    _circuitBreakerPolicy = new CircuitBreaker(faultThreshholdPerWindowDuration,
        //        faultWindowDurationInMilliseconds, circuitOpenDurationInSecs,
        //        () =>
        //        {
        //            //CloseRedisConnection();
        //            onCircuitOpened?.Invoke();
        //        },
        //        () =>
        //        {
        //            //ConnectToRedis(onException);
        //            onCircuitClosed?.Invoke();
        //        },
        //        onException);
        //}

        private void SetupConnectionConfiguration(string redisEndpoint, int connectionTimeout, int syncTimeout)
        {
            _redisConfigurationOptions = new ConfigurationOptions();
            _redisConfigurationOptions.EndPoints.Add(redisEndpoint);
            _redisConfigurationOptions.ClientName = "RedisRateLimiter";
            _redisConfigurationOptions.ConnectTimeout = connectionTimeout;
            _redisConfigurationOptions.SyncTimeout = syncTimeout;
            _redisConfigurationOptions.AbortOnConnectFail = false;
        }

        private void CloseRedisConnection()
        {            
            var task = _redisConnection?.CloseAsync();
        }

        private void ConnectToRedis(Action<Exception> onException)
        {
            _redisConnection = ConnectionMultiplexer.Connect(_redisConfigurationOptions);
            if (_redisConnection == null || !_redisConnection.IsConnected)
            {
                onException?.Invoke(new Exception("Could not connect to redis server"));
            }
        }

        /// <summary>
        /// Rate limits a request using to cache key provided
        /// </summary>
        /// <param name="requestId">The request identifier.</param>
        /// <param name="method">The request method</param>
        /// <param name="host">The host</param>
        /// <param name="routeTemplate">The route template.</param>
        /// <param name="allowedCallRates">The rate limit entry.</param>
        /// <returns></returns>

        public async Task<RateLimitingResult> LimitRequestAsync(string requestId, string method, string host,
            string routeTemplate,
            IList<AllowedCallRate> allowedCallRates)
        {
            return await _circuitBreakerPolicy.ExecuteAsync(async () =>
            {
                if (!_redisConnection.IsConnected)
                    throw new Exception("Redis is not connected at the moment");

                var redisDb = _redisConnection.GetDatabase();
                var redisTransaction = redisDb.CreateTransaction();
                var utcNowTicks = DateTime.UtcNow.Ticks;
                IList<Task<long>> numberOfRequestsMadePerAllowedCallRateAsync = new List<Task<long>>();

                IList<RateLimitCacheKey> cacheKeys = new List<RateLimitCacheKey>();
                foreach (var allowedCallRate in allowedCallRates)
                {
                    numberOfRequestsMadePerAllowedCallRateAsync.Add(
                        GetNumberOfRequestsAsync(requestId, method, host, routeTemplate, allowedCallRate, cacheKeys,
                            redisTransaction, utcNowTicks));
                }

                await ExecuteTransactionAsync(redisTransaction);
                
                var violatedCacheKeys = new SortedList<long, RateLimitCacheKey>();

                int minCallsRemaining = int.MaxValue;
                var minCallsCacheKey = default(RateLimitCacheKey);
                for (int i = 0; i < allowedCallRates.Count; i++)
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
                    return new RateLimitingResult(false, 0, minCallsCacheKey, minCallsRemaining);

                var postViolationTransaction = redisDb.CreateTransaction();

                if(!_countThrottledRequests)
                    UndoUnsuccessfulRequestCount(postViolationTransaction, cacheKeys, utcNowTicks);

                var violatedCacheKey = violatedCacheKeys.Last().Value;

                var setupGetOldestRequestTimestampInTicks =
                    SetupGetOldestRequestTimestampInTicks(postViolationTransaction,
                        violatedCacheKey, utcNowTicks);

                await ExecuteTransactionAsync(postViolationTransaction);

                var rateLimitingResult = new RateLimitingResult(true,
                    await GetWaitingIntervalInTicks(setupGetOldestRequestTimestampInTicks, 
                        violatedCacheKey, utcNowTicks), violatedCacheKey, 0);

                _onThrottled?.Invoke(rateLimitingResult);

                return rateLimitingResult;

            }, new RateLimitingResult(false, 0));
        }

        private async Task<long> GetWaitingIntervalInTicks(Task<SortedSetEntry[]> setupGetOldestRequestTimestampInTicks, 
            RateLimitCacheKey violatedCacheKey, 
            long utcNowTicks)
        {
            return await GetOldestRequestTimestampInTicks(setupGetOldestRequestTimestampInTicks,
                       violatedCacheKey, utcNowTicks).ConfigureAwait(false) +
                   (long) violatedCacheKey.Unit - utcNowTicks;
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
            long utcNowTicks)
        {
            foreach (var cacheKey in cacheKeys)
                UndoUnsuccessfulRequestCount(undoUnsuccessfulRequestCountTransaction, cacheKey, utcNowTicks);

        }

        /// <summary>
        /// Rate limits a request using to cache key provided
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <returns></returns>
        public Task<RateLimitingResult> LimitRequestAsync(RateLimitCacheKey cacheKey)
        {
            return LimitRequestAsync(cacheKey.RequestId, cacheKey.Method, cacheKey.Host, cacheKey.RouteTemplate,
                new List<AllowedCallRate>() { new AllowedCallRate(cacheKey.Limit, cacheKey.Unit) });
        }

        protected abstract Task<long> GetNumberOfRequestsAsync(
            string requestId,
            string method,
            string host,
            string routeTemplate,
            AllowedCallRate policy,
            IList<RateLimitCacheKey> cacheKeys,
            ITransaction redisTransaction,
            long utcNowTicks);

        protected abstract void UndoUnsuccessfulRequestCount(
            ITransaction postViolationTransaction,
            RateLimitCacheKey cacheKey,
            long utcNowTicks);

        protected abstract Task<SortedSetEntry[]> SetupGetOldestRequestTimestampInTicks(
            ITransaction postViolationTransaction,
            RateLimitCacheKey cacheKey,
            long utcNowTicks);

        protected abstract Task<long> GetOldestRequestTimestampInTicks(
            Task<SortedSetEntry[]> task, RateLimitCacheKey cacheKey,
            long utcNowTicks);

    }
}
