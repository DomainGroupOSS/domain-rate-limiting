using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Domain.RateLimiting.Core
{
    /// <summary>
    /// Policy for rate limiting api requests
    /// </summary>
    public class RateLimitingPolicyManager : IRateLimitingPolicyProvider
    {
        private const string AllRequestKeys = "*";
        private const string AllRequestPaths = "*";
        private const string AllHttpMethods = "*";

        private readonly IRateLimitingPolicyProvider _policyProvider;

        private readonly IDictionary<RateLimitingPolicyKey, RateLimitPolicy> _entries = new Dictionary<RateLimitingPolicyKey, RateLimitPolicy>();
        private readonly ICollection<string> _whiteListedPaths = new Collection<string>();
        private readonly ICollection<string> _whiteListedRequestKeys = new Collection<string>();

        private static readonly RateLimitingPolicyKey AllRequestsKey = new RateLimitingPolicyKey(AllRequestKeys, AllRequestPaths, AllHttpMethods);
        private static readonly RateLimitingPolicyKey AllGetRequestsKey = new RateLimitingPolicyKey(AllRequestKeys, AllRequestPaths, HttpMethod.Get.Method.ToUpperInvariant());
        private static readonly RateLimitingPolicyKey AllPutRequestsKey = new RateLimitingPolicyKey(AllRequestKeys, AllRequestPaths, HttpMethod.Put.Method.ToUpperInvariant());
        private static readonly RateLimitingPolicyKey AllPostRequestsKey = new RateLimitingPolicyKey(AllRequestKeys, AllRequestPaths, HttpMethod.Post.Method.ToUpperInvariant());
        private static readonly RateLimitingPolicyKey AllDeleteRequestsKey = new RateLimitingPolicyKey(AllRequestKeys, AllRequestPaths, HttpMethod.Delete.Method.ToUpperInvariant());
        private static readonly RateLimitingPolicyKey AllOptionsRquestsKey = new RateLimitingPolicyKey(AllRequestKeys, AllRequestPaths, HttpMethod.Options.Method.ToUpperInvariant());
        private static readonly RateLimitingPolicyKey AllHeadRquestsKey = new RateLimitingPolicyKey(AllRequestKeys, AllRequestPaths, HttpMethod.Head.Method.ToUpperInvariant());
        private static readonly RateLimitingPolicyKey AllTraceRquestsKey = new RateLimitingPolicyKey(AllRequestKeys, AllRequestPaths, HttpMethod.Trace.Method.ToUpperInvariant());
        private static readonly IDictionary<string, RateLimitingPolicyKey> AllRequestsByHttpMethodKeyMapping = new Dictionary<string, RateLimitingPolicyKey>
        {
            {HttpMethod.Delete.Method.ToUpperInvariant(), AllDeleteRequestsKey},
            {HttpMethod.Get.Method.ToUpperInvariant(), AllGetRequestsKey},
            {HttpMethod.Head.Method.ToUpperInvariant(), AllHeadRquestsKey},
            {HttpMethod.Options.Method.ToUpperInvariant(), AllOptionsRquestsKey},
            {HttpMethod.Post.Method.ToUpperInvariant(), AllPostRequestsKey},
            {HttpMethod.Put.Method.ToUpperInvariant(), AllPutRequestsKey},
            {HttpMethod.Trace.Method.ToUpperInvariant(), AllTraceRquestsKey},
        };

        /// <summary>
        /// Initializes a new instance of the &lt;see cref="T:System.Object" /&gt; class.
        /// </summary>
        /// <param name="policyProvider"></param>
        public RateLimitingPolicyManager(IRateLimitingPolicyProvider policyProvider)
        {
            _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        }


        /// <summary>
        /// Adds the policy for all requests and all HTTP methods.
        /// </summary>
        /// <param name="policies">The policies.</param>
        /// <param name="requestKey"></param>
        /// <param name="allowAttributeOverride"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public RateLimitingPolicyManager AddPoliciesForAllEndpoints(IList<AllowedCallRate> policies, 
            string requestKey=AllRequestKeys, bool allowAttributeOverride = false, string name = "")
        {
            AddEndpointPolicy(new RateLimitPolicy(requestKey, AllRequestPaths, AllHttpMethods, policies,
                allowAttributeOverride, name));

            return this;
        }

        /// <summary>
        /// Adds the policy for all requests limiting to the specified HTTP Method.
        /// </summary>
        /// <param name="policies">The policies.</param>
        /// <param name="httpMethod">The HTTP method.</param>
        /// <param name="requestKey"></param>
        /// <param name="allowAttributeOverride"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public RateLimitingPolicyManager AddPoliciesForAllEndpointsForSpecifiedMethod(IList<AllowedCallRate> policies, 
            string httpMethod, string requestKey = AllRequestKeys, bool allowAttributeOverride = false, string name = "")
        {
            AddEndpointPolicy(new RateLimitPolicy(requestKey, AllRequestPaths, AllHttpMethods, policies,
                allowAttributeOverride, name));

            return this;
        }

        /// <summary>
        /// Adds the policy for all get requests.
        /// </summary>
        /// <param name="policies">The policies.</param>
        /// <param name="requestKey"></param>
        /// <param name="allowAttributeOverride"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public RateLimitingPolicyManager AddPoliciesForAllGetRequests(IList<AllowedCallRate> policies, 
            string requestKey = AllRequestKeys, bool allowAttributeOverride = false, string name = "")
        {
            AddEndpointPolicy(new RateLimitPolicy(requestKey, AllRequestPaths, AllHttpMethods, policies,
                allowAttributeOverride, name));

            return this;
        }

        /// <summary>
        /// Adds the policy for all put requests.
        /// </summary>
        /// <param name="policies">The policies.</param>
        /// <param name="requestKey"></param>
        /// <param name="allowAttributeOverride"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public RateLimitingPolicyManager AddPoliciesForAllPutRequests(IList<AllowedCallRate> policies, 
            string requestKey = AllRequestKeys, bool allowAttributeOverride = false, string name = "")
        {
            AddEndpointPolicy(new RateLimitPolicy(requestKey, AllRequestPaths, AllHttpMethods, policies,
                allowAttributeOverride, name));

            return this;
        }

        /// <summary>
        /// Adds the policy for all put requests.
        /// </summary>
        /// <param name="policies">The policies.</param>
        /// <param name="requestKey"></param>
        /// <param name="allowAttributeOverride"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public RateLimitingPolicyManager AddPoliciesForAllPostRequests(IList<AllowedCallRate> policies, 
            string requestKey = AllRequestKeys, bool allowAttributeOverride = false, string name = "")
        {
            AddEndpointPolicy(new RateLimitPolicy(requestKey, AllRequestPaths, AllHttpMethods, policies,
                allowAttributeOverride, name));

            return this;
        }

        /// <summary>
        /// Adds the policy for all delete requests.
        /// </summary>
        /// <param name="policies">The policies.</param>
        /// <param name="requestKey"></param>
        /// <returns></returns>
        public RateLimitingPolicyManager AddPoliciesForAllDeleteRequests(IList<AllowedCallRate> policies, 
            string requestKey = AllRequestKeys, bool allowAttributeOverride = false, string name = "")
        {
            AddEndpointPolicy(new RateLimitPolicy(requestKey, AllRequestPaths, AllHttpMethods, policies,
                allowAttributeOverride, name));

            return this;
        }

        /// <summary>
        /// Adds the policy for all head requests.
        /// </summary>
        /// <param name="policies">The policies.</param>
        /// <param name="requestKey"></param>
        /// <param name="allowAttributeOverride"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public RateLimitingPolicyManager AddPoliciesForAllHeadRequests(IList<AllowedCallRate> policies, 
            string requestKey = AllRequestKeys, bool allowAttributeOverride = false, string name = "")
        {
            AddEndpointPolicy(new RateLimitPolicy(requestKey, AllRequestPaths, AllHttpMethods, policies,
                allowAttributeOverride, name));

            return this;
        }

        /// <summary>
        /// Adds the policy for all options requests.
        /// </summary>
        /// <param name="policies">The policies.</param>
        /// <param name="requestKey"></param>
        /// <param name="allowAttributeOverride"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public RateLimitingPolicyManager AddPoliciesForAllOptionsRequests(IList<AllowedCallRate> policies, 
            string requestKey = AllRequestKeys, bool allowAttributeOverride = false, string name = "")
        {
            AddEndpointPolicy(new RateLimitPolicy(requestKey, AllRequestPaths, AllHttpMethods, policies,
                allowAttributeOverride, name));

            return this;
        }

        /// <summary>
        /// Adds the policy for all trace requests.
        /// </summary>
        /// <param name="policies">The policies.</param>
        /// <param name="requestKey"></param>
        /// <param name="allowAttributeOverride"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public RateLimitingPolicyManager AddPoliciesForAllTraceRequests(IList<AllowedCallRate> policies, 
            string requestKey = AllRequestKeys, bool allowAttributeOverride = false, string name = "")
        {
            AddEndpointPolicy(new RateLimitPolicy(requestKey, AllRequestPaths, AllHttpMethods, policies,
                allowAttributeOverride, name));

            return this;
        }
        
        /// <summary>
        /// Adds the specified path to white list, any request paths starting with the specified value will not be rate limited.
        /// </summary>
        /// <param name="endpoint">The endpoint.</param>
        /// <returns></returns>
        public RateLimitingPolicyManager AddPathToWhiteList(string endpoint)
        {
            _whiteListedPaths.Add(endpoint);

            return this;
        }

        /// <summary>
        /// Adds the specified paths to white list, any request paths starting with the specified values will not be rate limited.
        /// </summary>
        /// <param name="endpoints">The endpoints.</param>
        /// <returns></returns>
        public RateLimitingPolicyManager AddPathsToWhiteList(IEnumerable<string> endpoints)
        {
            if (endpoints == null)
                return this;

            foreach (var e in endpoints)
            {
                _whiteListedPaths.Add(e);
            }

            return this;
        }

        public RateLimitingPolicyManager AddRequestKeysToWhiteList(IEnumerable<string> requestKeys)
        {
            if (requestKeys == null)
                return this;

            foreach (var e in requestKeys)
            {
                _whiteListedRequestKeys.Add(e);
            }

            return this;
        }

        /// <summary>
        /// Gets the rate limiting policy entry for the current request.
        /// </summary>
        /// <remarks>
        /// Rate limiting matching occurs in the following order
        /// 1. Match on the current http method and request path e.g. 'GET /v1/example'
        /// 2. If no match found in (1), match on all http methods (*) and the request path e.g. 'GET /v1/example'
        /// 3. if no match found in (1, 2), match on the current http method for all request paths (*) e.g. 'GET /*'
        /// 4. If no match found in (1, 2, 3), match on all http methods (*) and all request paths (*) i.e. '* /*'
        /// </remarks>
        /// <param name="rateLimitingRequest">The request path or endpoint</param>
        /// <returns>
        /// <c>A rate limiting policy entry</c> if [one is found matching the current request]; 
        /// otherwise, <c>null</c>.
        /// </returns>
        public async Task<RateLimitPolicy> GetPolicyAsync(RateLimitingRequest rateLimitingRequest)
        {
            if (IsWhiteListedPath(rateLimitingRequest.Path, rateLimitingRequest.Method))
                return null;

            var providedPolicyEntry = await _policyProvider.GetPolicyAsync(rateLimitingRequest);

            if (providedPolicyEntry == null || 
                IsWhiteListedRequestKey(providedPolicyEntry.RequestKey))
                return null;

            if (providedPolicyEntry.AllowedCallRates != null && providedPolicyEntry.AllowedCallRates.Any())
                return providedPolicyEntry;
            
            var policyKey = new RateLimitingPolicyKey(providedPolicyEntry.RequestKey,
                rateLimitingRequest.RouteTemplate, rateLimitingRequest.Method);

            if (!_entries.ContainsKey(policyKey))
            {
                // Policy key for the current request path belonging to all HTTP methods, e.g. * /v1/example
                // Policy key matching the current request path for current HTTP method, e.g. GET /v1/example
                policyKey = new RateLimitingPolicyKey(providedPolicyEntry.RequestKey,
                    rateLimitingRequest.RouteTemplate, AllHttpMethods);
            }

            if (!_entries.ContainsKey(policyKey))
            {
                // Policy key for the current request path belonging to all HTTP methods, e.g. * /v1/example
                // Policy key matching the current request path for current HTTP method, e.g. GET /v1/example
                policyKey = new RateLimitingPolicyKey(providedPolicyEntry.RequestKey,
                    AllRequestPaths, AllHttpMethods);
            }

            if (!_entries.ContainsKey(policyKey))
            {
                // Policy key for the current request path belonging to all HTTP methods, e.g. * /v1/example
                // Policy key matching the current request path for current HTTP method, e.g. GET /v1/example
                policyKey = new RateLimitingPolicyKey(AllRequestKeys,
                    rateLimitingRequest.RouteTemplate, rateLimitingRequest.Method);
            }
            
            if (!_entries.ContainsKey(policyKey))
            {
                // Policy key for the current request path belonging to all HTTP methods, e.g. * /v1/example
                policyKey = new RateLimitingPolicyKey(AllRequestKeys, 
                    rateLimitingRequest.RouteTemplate, AllHttpMethods);
            }

            if (!_entries.ContainsKey(policyKey))
            {
                // Policy key for all requests paths matching the current HTTP method, e.g.. GET /*
                policyKey = AllRequestsByHttpMethodKeyMapping[rateLimitingRequest.Method.ToUpperInvariant()];
            }

            if (!_entries.ContainsKey(policyKey))
            {
                // Policy key for all clients, all requests paths and all HTTP methods, i.e. * /*
                policyKey = AllRequestsKey;
            }

            return _entries.ContainsKey(policyKey) ? 
                new RateLimitPolicy(providedPolicyEntry.RequestKey, 
                _entries[policyKey].RouteTemplate, _entries[policyKey].HttpMethod,
                _entries[policyKey].AllowedCallRates, _entries[policyKey].AllowAttributeOverride,
                _entries[policyKey].Name) : null;
        }

        /// <summary>
        /// Policies the exists for request.
        /// </summary>
        /// <param name="requestKey">The request key (for example the client_id from ClaimsPrincipal.</param>
        /// <param name="requestPath">The request path.</param>
        /// <param name="httpMethod">The httpMethod like GET.</param>
        /// <returns></returns>
        public bool PolicyExists(string requestKey, string requestPath, string httpMethod)
        {
            var key = new RateLimitingPolicyKey(requestKey, requestPath, httpMethod);

            return _entries.ContainsKey(key) || _entries.ContainsKey(AllRequestsKey);
        }

        /// <summary>
        /// Determines whether [is white listed request] [the specified request].
        /// </summary>
        /// <param name="requestPath">The request path.</param>
        /// <param name="httpMethod">The http method.</param>
        /// <returns></returns>
        public bool IsWhiteListedPath(string requestPath, string httpMethod)
        {
            return _whiteListedPaths.Any(requestPath.StartsWith);
        }

        public bool IsWhiteListedRequestKey(string requestKey)
        {
            return _whiteListedRequestKeys.Any(rk =>
                string.Equals(rk, requestKey, StringComparison.CurrentCultureIgnoreCase));
        }

        public RateLimitingPolicyManager AddEndpointPolicies(IEnumerable<RateLimitPolicy> policies)
        {
            foreach (var policy in policies)
            {
                AddEndpointPolicy(policy);
            }

            return this;
        }

        public RateLimitingPolicyManager AddEndpointPolicy(RateLimitPolicy policy)
        {
            var entry = policy;

            if (_entries.ContainsKey(entry.Key))
                throw new InvalidOperationException($"Rate limit policy for {entry.Key} requests has already been defined.");

            _entries.Add(entry.Key, entry);

            return this;
        }
    }
}