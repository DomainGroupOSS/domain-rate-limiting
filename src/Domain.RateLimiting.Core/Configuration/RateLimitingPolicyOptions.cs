using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.RateLimiting.Core.Configuration
{
    public class RateLimitingPolicyOptions
    {
        public IDictionary<string, int> AllowedCallRates { get; set; } = new Dictionary<string, int>();
        public bool AllowAttributeOverride { get; set; } = false;
        public string Name { get; set; } = "";
        public string RouteTemplate { get; set; } = "*";
        public string HttpMethod { get; set; } = "*";
        public string RequestKey { get; set; } = "*";
    }
}
