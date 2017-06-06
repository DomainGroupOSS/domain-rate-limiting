namespace Domain.RateLimiting.Core
{
    public class RateLimitingResult
    {
        public readonly bool Throttled;

        public readonly long WaitingIntervalInTicks;

        public readonly RateLimitCacheKey CacheKey;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="cacheKey"></param>
        /// <param name="throttled"></param>
        /// <param name="waitingIntervalInTicks"></param>
        public RateLimitingResult(bool throttled, long waitingIntervalInTicks, RateLimitCacheKey cacheKey)
        {
            Throttled = throttled;
            WaitingIntervalInTicks = waitingIntervalInTicks;
            CacheKey = cacheKey;
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
