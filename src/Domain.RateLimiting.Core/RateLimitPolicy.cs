using System;
using System.Collections.Generic;
using System.Linq;

namespace Domain.RateLimiting.Core
{
    /// <summary>
    /// Represents the rate limiting policy
    /// </summary>
    public class RateLimitPolicy
    {
        public const string AllRequestKeys = "*";
        public const string AllHttpMethods = "*";
        public const string AllRequestPaths = "*";

        public RateLimitPolicy(string requestKey, IList<AllowedCallRate> policies,
            bool allowAttributeOverride = false, string name = "") :
            this(requestKey, AllRequestPaths, AllHttpMethods, policies, allowAttributeOverride, name)
        { }
        public RateLimitPolicy(string requestKey, bool allowAttributeOverride = false, string name = "") :
            this(requestKey, AllRequestPaths, AllHttpMethods, new List<AllowedCallRate>(),
                allowAttributeOverride, name)
        { }

        public RateLimitPolicy(string requestKey, string httpMethod, IList<AllowedCallRate> policies,
            bool allowAttributeOverride = false, string name = "") :
            this(requestKey, AllRequestPaths, httpMethod, policies, allowAttributeOverride, name)
        { }

        public RateLimitPolicy(string requestKey, IList<AllowedCallRate> policies, string routeTemplate,
            bool allowAttributeOverride = false, string name = "") :
            this(requestKey, routeTemplate, AllHttpMethods, policies, allowAttributeOverride, name)
        { }


        public RateLimitPolicy(string requestKey, string routeTemplate, string httpMethod,
            IList<AllowedCallRate> allowedCallRates, bool allowAttributeOverride = false, string name = "")
        {
            if (string.IsNullOrWhiteSpace(requestKey)) throw new ArgumentNullException(nameof(requestKey),
                "requestKey cannot be null or whitespace");

            if (requestKey.Length == 0) throw new ArgumentOutOfRangeException(nameof(requestKey),
                "requestKey cannot be empty");

            if (string.IsNullOrWhiteSpace(routeTemplate) || routeTemplate.Length == 0)
                routeTemplate = AllRequestPaths;

            if (string.IsNullOrWhiteSpace(httpMethod))
                httpMethod = AllHttpMethods;

            RequestKey = requestKey;
            RouteTemplate = routeTemplate;
            HttpMethod = httpMethod;
            AllowedCallRates = allowedCallRates;
            AllowAttributeOverride = allowAttributeOverride;
            Name = name;
            Key = new RateLimitingPolicyKey(RequestKey, routeTemplate, httpMethod);
        }

        /// <summary>
        /// Gets the policies hash key.
        /// </summary>
        /// <value>The policies hash key.
        /// </value>
        public RateLimitingPolicyKey Key { get; }

        /// <summary>
        /// The policies to apply
        /// </summary>
        /// <value>The policies to apply</value>
        public IList<AllowedCallRate> AllowedCallRates { get; }

        public bool AllowAttributeOverride { get; }
        public string Name { get; }

        /// <summary>
        /// Gets the path to apply the specified rate limit</summary>
        /// <value>The path to rate limit.</value>
        public string RouteTemplate { get; }

        /// <summary>
        /// Get the http method to limit on for the specified path
        /// </summary>
        /// <value>The HTTP method.</value>
        public string HttpMethod { get; }

        public string RequestKey { get; }
    }
}