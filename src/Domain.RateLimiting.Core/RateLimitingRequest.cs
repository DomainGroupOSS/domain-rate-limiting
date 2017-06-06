using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;

namespace Domain.RateLimiting.Core
{
    public class RateLimitingRequest
    {
        public string Path { get; }
        public string Method { get; }
        public Func<IDictionary<string, string[]>> GetHeaders { get; }
        public ClaimsPrincipal ClaimsPrincipal { get; }
        public Stream InputStream { get; }
        public RateLimitingRequest(string path, string method, Func<IDictionary<string, string[]>> getHeaders, ClaimsPrincipal claimsPrincipal,
            Stream inputStream)
        {
            Path = path;
            Method = method;
            GetHeaders = getHeaders;
            ClaimsPrincipal = claimsPrincipal;
            InputStream = inputStream;
        }
    }
}
