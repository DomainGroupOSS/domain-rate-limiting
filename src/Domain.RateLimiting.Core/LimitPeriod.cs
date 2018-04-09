using System;

namespace Domain.RateLimiting.Core
{
    public class LimitPeriod
    {
        public DateTime StartDateTimeUtc { get;  }
        public TimeSpan Duration { get;  }
        public bool OnGoing { get; } = true;

        public LimitPeriod(DateTime startDateTimeUtc, int durationInSecs, bool onGoing)
        {
            StartDateTimeUtc = startDateTimeUtc;
            Duration = new TimeSpan(0, 0, durationInSecs);
            OnGoing = onGoing;
        }
    }
}