namespace Domain.RateLimiting.Core
{
    public class RateLimitingResult
    {
        public bool Throttled { get; }

        public long WaitingIntervalInTicks { get; }

        public RateLimitCacheKey CacheKey { get; }
        public int CallUnitsRemaining { get; }
        public string ViolatedPolicyName { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cacheKey"></param>
        /// <param name="throttled"></param>
        /// <param name="waitingIntervalInTicks"></param>
        /// <param name="callsRemaining"></param>
        /// <param name="violatedPolicyName"></param>
        public RateLimitingResult(bool throttled, long waitingIntervalInTicks, RateLimitCacheKey cacheKey, int callsRemaining, string violatedPolicyName = "")
        {
            Throttled = throttled;
            WaitingIntervalInTicks = waitingIntervalInTicks;
            CacheKey = cacheKey;
            CallUnitsRemaining = callsRemaining;
            ViolatedPolicyName = violatedPolicyName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="throttled"></param>
        /// <param name="waitingIntervalInTicks"></param>
        public RateLimitingResult(bool throttled, long waitingIntervalInTicks)
        {
            Throttled = throttled;
            WaitingIntervalInTicks = waitingIntervalInTicks;
        }
    }
}
