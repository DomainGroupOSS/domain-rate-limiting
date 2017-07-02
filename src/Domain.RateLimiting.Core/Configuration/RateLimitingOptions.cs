using System;
using System.Collections.Generic;

namespace Domain.RateLimiting.Core.Configuration
{
    public class RateLimitingOptions
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="RateLimitingOptions" /> class.
        /// </summary>
        public RateLimitingOptions()
        {
        }

        private IList<RateLimitPolicy> _rateLimitPolicies;

        private IEnumerable<RateLimitPolicy> ParseRateLimitPolicyStringsAndOptions()
        {
            _rateLimitPolicies = new List<RateLimitPolicy>();
            foreach (var policyString in RateLimitPolicyStrings)
            {
                var policyStringParameters = policyString.Split(new char[] { ':' });
                if (policyStringParameters.Length != 6)
                    throw new ArgumentException(
                        "The policy config is not valid...must be of form RequestKey:HttpMethod:RouteTemplate:AllowedCallRates:AllowAttributeOverride:PolicyName");

                var allowedRatesStrings = policyStringParameters[3]
                    .Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);

                IList<AllowedCallRate> allowedRates = new List<AllowedCallRate>();
                foreach (var allowedRateString in allowedRatesStrings)
                {
                    var allowedRateParameters = allowedRateString.Split(new char[] { '_' });
                    if (allowedRateParameters.Length != 2)
                        throw new ArgumentException(
                            "The allowed rate format is not valid...must be of form 60_PerMinute&200_PerHour");

                    allowedRates.Add(new AllowedCallRate(int.Parse(allowedRateParameters[0]),
                        (RateLimitUnit)Enum.Parse(typeof(RateLimitUnit), allowedRateParameters[1])));
                }

                _rateLimitPolicies.Add(new RateLimitPolicy(policyStringParameters[0],
                    policyStringParameters[2],
                    policyStringParameters[1], allowedRates,
                    bool.Parse(policyStringParameters[4]),
                    policyStringParameters[5]));
            }

            foreach (var policyOption in RateLimitPolicyOptions)
            {
                IList<AllowedCallRate> allowedRates = new List<AllowedCallRate>();
                foreach (var allowedCallRate in policyOption.AllowedCallRates)
                {
                    allowedRates.Add(new AllowedCallRate(allowedCallRate.Value, 
                        (RateLimitUnit)Enum.Parse(typeof(RateLimitUnit), allowedCallRate.Key)));
                }
                _rateLimitPolicies.Add(new RateLimitPolicy(policyOption.RequestKey,
                    policyOption.RouteTemplate, policyOption.HttpMethod, allowedRates, 
                    policyOption.AllowAttributeOverride, policyOption.Name));
            }

            return _rateLimitPolicies;
        }

        public bool RateLimitingEnabled { get; set; }
        
        public IList<string> RateLimitPolicyStrings { get; set; } = 
            new List<string>();

        public IList<RateLimitingPolicyOptions> RateLimitPolicyOptions { get; set; } = 
            new List<RateLimitingPolicyOptions>();
        
        public IEnumerable<RateLimitPolicy> RateLimitPolicies => 
            _rateLimitPolicies ?? ParseRateLimitPolicyStringsAndOptions();

        /// <summary>
        ///     Gets the rate limiting white listed paths.
        /// </summary>
        /// <value>
        ///     The rate limiting white listed paths.
        /// </value>
        public IList<string> RateLimitingWhiteListedPaths { get; set; } = 
            new List<string>();

        /// <summary>
        ///     Gets the rate limiting white listed paths.
        /// </summary>
        /// <value>
        ///     The rate limiting white listed paths.
        /// </value>
        public IList<string> RateLimitingWhiteListedRequestKeys { get; set; } = 
            new List<string>();
    }
}