using System.Threading.Tasks;

namespace Domain.RateLimiting.Core
{
    public interface IRateLimitingPolicyParametersProvider
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="rateLimitingRequest"></param>
        /// <returns>RateLimitPolicyEntry</returns>
        Task<RateLimitPolicyParameters> GetPolicyParametersAsync(RateLimitingRequest rateLimitingRequest);
    }
}
