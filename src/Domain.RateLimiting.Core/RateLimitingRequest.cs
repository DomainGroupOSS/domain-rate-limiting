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
        public Func<string, string[]> GetHeader { get; }
        public ClaimsPrincipal ClaimsPrincipal { get; }
        public Stream InputStream { get; }

        public RateLimitingRequest(string routeTemplate, string path, string method, Func<string, string[]> getHeader, ClaimsPrincipal claimsPrincipal,
            Stream inputStream)
        {
            RouteTemplate = routeTemplate;
            Path = path;
            Method = method;
            GetHeader = getHeader;
            ClaimsPrincipal = claimsPrincipal;
            InputStream = inputStream;
        }
    }
}
