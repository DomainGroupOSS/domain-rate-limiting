using System.Threading.Tasks;

namespace Domain.RateLimiting.Core
{
    public interface IRateLimitingPolicyProvider
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="rateLimitingRequest"></param>
        /// <returns>RateLimitPolicyEntry</returns>
        Task<RateLimitPolicy> GetPolicyAsync(RateLimitingRequest rateLimitingRequest);
    }
}
