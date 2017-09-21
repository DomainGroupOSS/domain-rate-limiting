using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.RateLimiting.Core
{
    public interface IClock
    {
        long GetCurrentUtcTimeInTicks();
        DateTime GetUtcDateTime();
    }


    public class DefaultClock : IClock
    {
        public long GetCurrentUtcTimeInTicks()
        {
            return DateTime.UtcNow.Ticks;
        }

        public DateTime GetUtcDateTime()
        {
            return DateTime.UtcNow;
        }
    }
}
