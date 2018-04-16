namespace Domain.RateLimiting.Core
{
    public enum ResultState
    {
        Success,
        Throttled,
        NotApplicable,
        LimitApplicationFailed,
        ThrottledButCompensationFailed
    }
}