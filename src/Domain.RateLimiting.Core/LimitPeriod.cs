using System;

namespace Domain.RateLimiting.Core
{
    public class LimitPeriod
    {
        public DateTime StartDateUtc { get;  }
        public TimeSpan Duration { get;  }
        public bool Rolling { get;  }

        public LimitPeriod(DateTime startDateUtc, int durationInSecs, bool rolling)
        {
            StartDateUtc = startDateUtc;
            Duration = new TimeSpan(0, 0, durationInSecs);
            Rolling = rolling;
        }
    }
}