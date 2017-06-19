using System.Collections.Generic;

namespace Domain.RateLimiting.Core
{
    public class ThrottledResponseParameters
    {
        public string Message { get; }
        public IReadOnlyDictionary<string, string> RateLimitHeaders { get; }
        

        public static readonly int StatusCode = 429;

        public ThrottledResponseParameters(string message, 
            IReadOnlyDictionary<string, string> rateLimitHeaders)
        {
            Message = message;
            RateLimitHeaders = rateLimitHeaders;
        }
    }
}
