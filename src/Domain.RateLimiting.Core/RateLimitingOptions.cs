using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Domain.RateLimiting.Core
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

        private IEnumerable<RateLimitPolicy> ParseRateLimitPolicyStrings()
        {
            _rateLimitPolicies = new List<RateLimitPolicy>();
            foreach (var policyString in RateLimitPolicyStrings)
            {
                var policyStringParameters = policyString.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (policyStringParameters.Length != 6)
                    throw new ArgumentException(
                        "The policy config is not valid...must be of form client_0:GET:api/values/{id}:60_m&200_h:false:StaticPolicy_0");

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

            return RateLimitPolicies;
        }

        public bool RateLimitingEnabled { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string ThrottledResponseMessageToAppend { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public IEnumerable<string> RateLimitPolicyStrings { get; set; }


        public IEnumerable<RateLimitPolicy> RateLimitPolicies => _rateLimitPolicies ?? ParseRateLimitPolicyStrings();

        /// <summary>
        ///     Gets the rate limiting white listed paths.
        /// </summary>
        /// <value>
        ///     The rate limiting white listed paths.
        /// </value>
        public IEnumerable<string> RateLimitingWhiteListedPaths { get; set; }

        /// <summary>
        ///     Gets the rate limiting white listed paths.
        /// </summary>
        /// <value>
        ///     The rate limiting white listed paths.
        /// </value>
        public IEnumerable<string> RateLimitingWhiteListedRequestKeys { get; set; }
    }
}