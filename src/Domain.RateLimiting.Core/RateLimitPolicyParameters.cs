using System;
using System.Collections.Generic;

namespace Domain.RateLimiting.Core
{
    /// <summary>
    /// Represents the rate limiting policy for a single endpoint
    /// </summary>
    public class RateLimitPolicyParameters
    {
        private const string AllHttpMethods = "*";
        private const string AllRequestPaths = "*";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="requestKey"></param> 
        /// <param name="policies"></param>
        public RateLimitPolicyParameters(string requestKey, IList<RateLimitPolicy> policies) : 
            this(requestKey, AllRequestPaths, AllHttpMethods, policies)
        { }
        public RateLimitPolicyParameters(string requestKey) :
            this(requestKey, AllRequestPaths, AllHttpMethods, new List<RateLimitPolicy>())
        { }

        public RateLimitPolicyParameters(string requestKey, string httpMethod, IList<RateLimitPolicy> policies) :
            this(requestKey, AllRequestPaths, httpMethod, policies)
        { }

        public RateLimitPolicyParameters(string requestKey, IList<RateLimitPolicy> policies, string path) :
            this(requestKey, path, AllHttpMethods, policies)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object" /> class.
        /// </summary>
        /// <param name="requestKey">The requestKey provided by the client.</param>
        /// <param name="path">The path.</param>
        /// <param name="policies">The policies.</param>
        /// <param name="httpMethod">The HTTP method.</param>
        /// <exception cref="ArgumentOutOfRangeException">limit</exception>
        /// <exception cref="ArgumentNullException"><paramref name="path" /> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentOutOfRangeException">limit</exception>
        public RateLimitPolicyParameters(string requestKey, string path, string httpMethod, IList<RateLimitPolicy> policies)
        {
            if (string.IsNullOrWhiteSpace(requestKey)) throw new ArgumentNullException(nameof(requestKey), 
                "requestKey cannot be null or whitespace");

            if (requestKey.Length == 0) throw new ArgumentOutOfRangeException(nameof(requestKey), 
                "requestKey cannot be empty");

            if (path == null) throw new ArgumentNullException(nameof(path));
            if (path.Length == 0) throw new ArgumentOutOfRangeException(nameof(path), "the path to rate limit cannot be empty");
            if (string.IsNullOrWhiteSpace(httpMethod)) httpMethod = AllHttpMethods;

            RequestKey = requestKey;
            Path = path;
            HttpMethod = httpMethod;
            Policies = policies;
            Key = new RateLimitingPolicyParametersKey(RequestKey, path, httpMethod);
        }

        /// <summary>
        /// Gets the policies hash key.
        /// </summary>
        /// <value>The policies hash key.
        /// </value>
        public RateLimitingPolicyParametersKey Key { get; }

        /// <summary>
        /// The policies to apply
        /// </summary>
        /// <value>The policies to apply</value>
        public IList<RateLimitPolicy> Policies { get; }

        /// <summary>
        /// Gets the path the path to apply the specified rate limit</summary>
        /// <value>The path to rate limit.</value>
        public string Path { get; }

        /// <summary>
        /// Get the http method to limit on for the specified path
        /// </summary>
        /// <value>The HTTP method.</value>
        public string HttpMethod { get; }

        public string RequestKey { get; }
    }
}