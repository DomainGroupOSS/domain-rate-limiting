namespace Domain.RateLimiting.Core
{
    public class RateLimitingResult
    {
        public bool Throttled { get; }

        public long WaitingIntervalInTicks { get; }

        public RateLimitCacheKey CacheKey { get; }
        public int CallsRemaining { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cacheKey"></param>
        /// <param name="throttled"></param>
        /// <param name="waitingIntervalInTicks"></param>
        /// <param name="callsRemaining"></param>
        public RateLimitingResult(bool throttled, long waitingIntervalInTicks, RateLimitCacheKey cacheKey, int callsRemaining)
        {
            Throttled = throttled;
            WaitingIntervalInTicks = waitingIntervalInTicks;
            CacheKey = cacheKey;
            CallsRemaining = callsRemaining;
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
