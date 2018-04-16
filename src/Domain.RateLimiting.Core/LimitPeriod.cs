using System;

namespace Domain.RateLimiting.Core
{
    public class LimitPeriod
    {
        public DateTime StartDateTimeUtc { get;  }
        public TimeSpan Duration { get;  }
        public bool Repeating { get; } = true;

        public LimitPeriod(DateTime startDateTimeUtc, int durationInSecs, bool repeating)
        {
            StartDateTimeUtc = startDateTimeUtc;
            Duration = new TimeSpan(0, 0, durationInSecs);
            Repeating = repeating;
        }
    }
}