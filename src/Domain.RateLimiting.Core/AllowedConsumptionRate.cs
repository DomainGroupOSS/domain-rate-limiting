using System;
using System.Collections.Generic;
using System.Linq;

namespace Domain.RateLimiting.Core
{
    /// <summary>
    /// Rate Limit Policy Attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public class AllowedConsumptionRate : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Attribute" /> class.
        /// </summary>
        /// <param name="limit">The limit.</param>
        /// <param name="unit">The unit.</param>
        public AllowedConsumptionRate(int limit, RateLimitUnit unit)
        {
            if (limit <= 0)
                throw new ArgumentOutOfRangeException($"{nameof(limit)} has to be greater than 0");

            Limit = limit;
            Unit = unit;
        }
        
        public AllowedConsumptionRate(int limit, RateLimitUnit unit, LimitPeriod period):this(limit, unit)
        {
            Period = period;
        }

        public AllowedConsumptionRate(int limit, RateLimitUnit unit, int maxBurst) : this(limit, unit)
        {
            MaxBurst = maxBurst;
        }

        public AllowedConsumptionRate(int limit, RateLimitUnit unit, int maxBurst, LimitPeriod period) : this(limit, unit, maxBurst)
        {
            Period = period;
        }

        /// <summary>
        /// Gets the limit.
        /// </summary>
        /// <value>The limit.</value>
        public int Limit { get; }

        /// <summary>
        /// Gets the unit.
        /// </summary>
        /// <value>The unit.</value>
        public RateLimitUnit Unit { get; }
        
        public LimitPeriod Period { get; }
        public int MaxBurst { get; }

        public override string ToString()
        {
            return $"{Limit} tokens {Unit}";
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType()) return false;

            var compareObj = (AllowedConsumptionRate)obj;

            return compareObj.ToString() == ToString();
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }
    }
}