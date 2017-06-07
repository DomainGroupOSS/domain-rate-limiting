using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;

namespace Domain.RateLimiting.Core
{
    public class RateLimitingRequest
    {
        public string RouteTemplate { get; }
        public string Path { get; }
        public string Method { get; }
        public Func<IDictionary<string, string[]>> GetHeaders { get; }
        public ClaimsPrincipal ClaimsPrincipal { get; }
        public Stream InputStream { get; }

        public RateLimitingRequest(string routeTemplate, string path, string method, Func<IDictionary<string, string[]>> getHeaders, ClaimsPrincipal claimsPrincipal,
            Stream inputStream)
        {
            RouteTemplate = routeTemplate;
            Path = path;
            Method = method;
            GetHeaders = getHeaders;
            ClaimsPrincipal = claimsPrincipal;
            InputStream = inputStream;
        }
    }
}
