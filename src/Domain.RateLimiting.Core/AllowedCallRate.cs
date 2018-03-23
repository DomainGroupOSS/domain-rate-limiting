using System;
using System.Collections.Generic;
using System.Linq;

namespace Domain.RateLimiting.Core
{
    /// <summary>
    /// Rate Limit Policy Attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public class AllowedCallRate : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Attribute" /> class.
        /// </summary>
        /// <param name="limit">The limit.</param>
        /// <param name="unit">The unit.</param>
        public AllowedCallRate(int limit, RateLimitUnit unit)
        {
            if (limit <= 0)
                throw new ArgumentOutOfRangeException($"{nameof(limit)} has to be greater than 0");

            Limit = limit;
            Unit = unit;
        }


        public AllowedCallRate(int limit, RateLimitUnit unit, LimitPeriod period):this(limit, unit)
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

        public int Cost { get; set; } = 1;




        public override string ToString()
        {
            return $"{Limit} calls {Unit}";
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType()) return false;

            var compareObj = (AllowedCallRate)obj;

            return compareObj.ToString() == ToString();
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }
    }
}