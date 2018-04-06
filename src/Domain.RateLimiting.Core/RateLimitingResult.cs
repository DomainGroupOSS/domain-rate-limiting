namespace Domain.RateLimiting.Core
{
    public class RateLimitingResult
    {
        public ResultState State { get; }

        public bool Throttled { get; }

        public long WaitingIntervalInTicks { get; }

        public RateLimitCacheKey CacheKey { get; }
        public int TokensRemaining { get; }
        public string ViolatedPolicyName { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cacheKey"></param>
        /// <param name="throttled"></param>
        /// <param name="waitingIntervalInTicks"></param>
        /// <param name="callsRemaining"></param>
        /// <param name="violatedPolicyName"></param>
        public RateLimitingResult(ResultState state, long waitingIntervalInTicks, RateLimitCacheKey cacheKey, int callsRemaining, string violatedPolicyName = "")
        {
            State = state;
            WaitingIntervalInTicks = waitingIntervalInTicks;
            CacheKey = cacheKey;
            TokensRemaining = callsRemaining;
            ViolatedPolicyName = violatedPolicyName;
        }

        public RateLimitingResult(ResultState state)
        {
            State = state;
        }
    }
}
