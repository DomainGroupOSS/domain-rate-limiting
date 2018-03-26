namespace Domain.RateLimiting.Core
{
    public static class RateLimitHeaders
    {
        public const string RetryAfter = "Retry-After";
        public const string ViolatedPolicyName = "X-RateLimit-VPolicyName";
        public const string ViolatedCallRate = "X-RateLimit-VCallRate";
        public const string TokensRemaining = "X-RateLimit-Remaining";
        public const string Limit = "X-RateLimit-Limit";
    }
}
