using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.RateLimiting.Core
{
    public class RateLimitingPolicyParametersProvider : IRateLimitingPolicyParametersProvider
    {
        public async Task<RateLimitPolicyParameters> GetPolicyParametersAsync(RateLimitingRequest rateLimitingRequest)
        {
            var clientId = rateLimitingRequest.ClaimsPrincipal?.Claims.FirstOrDefault(c => c.Type == "client_id");

            if (string.IsNullOrWhiteSpace(clientId?.Value)) return null;

            return await Task.FromResult(new RateLimitPolicyParameters(clientId.Value));
        }
    }
}
