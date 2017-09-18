using System;

namespace Domain.RateLimiting.Core
{
    /// <summary>
    /// The hash key of a rate limit policy entry
    /// </summary>
    public struct RateLimitingPolicyKey
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="RateLimitingPolicyKey"/> struct.
        /// </summary>
        /// <param name="requestKey">The request path.</param>
        /// <param name="routeTemplate">The request path.</param>
        /// <param name="httpMethod">The HTTP method.</param>
        /// <exception cref="ArgumentNullException">requestPath or httpMethod</exception>
        /// <exception cref="ArgumentOutOfRangeException">requestPath;requestPath cannot be empty or httpMethod;httpMethod cannot be empty</exception>
        public RateLimitingPolicyKey(string requestKey, string routeTemplate, string httpMethod)
        {
            if (string.IsNullOrWhiteSpace(requestKey)) throw new ArgumentNullException(nameof(requestKey),
                $"{nameof(requestKey)} cannot be null or whitespace");
            if (requestKey.Length == 0) throw new ArgumentOutOfRangeException($"{nameof(requestKey)} cannot be empty");
            if (routeTemplate == null) throw new ArgumentNullException(nameof(routeTemplate));
            if (routeTemplate.Length == 0) throw new ArgumentOutOfRangeException(nameof(routeTemplate), "requestPath cannot be empty");
            if (httpMethod == null) throw new ArgumentNullException(nameof(httpMethod));
            if (httpMethod.Length == 0) throw new ArgumentOutOfRangeException(nameof(httpMethod), "httpMethod cannot be empty");

            RequestKey = requestKey;
            RouteTemplate = (routeTemplate.StartsWith(@"/") ? routeTemplate : @"/" + routeTemplate).ToLowerInvariant();
            HttpMethod = httpMethod.ToUpperInvariant();
        }

        /// <summary>
        /// The unique request key supplied by the client...this can be the client_id
        /// </summary>
        public readonly string RequestKey;
        /// <summary>
        /// The endpoint being rate limited
        /// </summary>
        public readonly string RouteTemplate;

        /// <summary>
        /// The HTTP method being rate limited
        /// </summary>
        public readonly string HttpMethod;

        
        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        public override string ToString()
        {
            return $"{RequestKey}::{HttpMethod.ToUpperInvariant()} {RouteTemplate.ToLowerInvariant()}";
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param>
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType()) return false;

            var compareObj = (RateLimitingPolicyKey)obj;

            return compareObj.ToString() == ToString();
        }
    }
}