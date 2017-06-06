namespace Domain.RateLimiting.Core
{
    public class RateLimitedResponseParameters
    {
        public readonly string Message;
        public string RetryAfterHeader { get; set; }
        
        public readonly string RetryAfterInSecs;

        public static readonly int StatusCode = 429;

        public RateLimitedResponseParameters(string message, string retryAfterHeader, string retryAfterInSeconds)
        {
            Message = message;
            RetryAfterHeader = retryAfterHeader;
            RetryAfterInSecs = retryAfterInSeconds;
        }
    }
}
