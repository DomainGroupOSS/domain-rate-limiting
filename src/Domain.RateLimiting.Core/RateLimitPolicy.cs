using System;
using System.Collections.Generic;
using System.Linq;

namespace Domain.RateLimiting.Core
{
    /// <summary>
    /// Rate Limit Policy Attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public class RateLimitPolicy : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Attribute" /> class.
        /// </summary>
        /// <param name="limit">The limit.</param>
        /// <param name="unit">The unit.</param>
        public RateLimitPolicy(int limit, RateLimitUnit unit)
        {
#if NETSTANDARD1_3
            //this is a test
#elif NET452
            // not a test
#endif
            if (limit <= 0) throw new ArgumentOutOfRangeException($"{nameof(limit)} has to be greater than 0");

            Limit = limit;
            Unit = unit;
            WhiteListRequestKeys = Enumerable.Empty<string>();
        }

        //public object TypeId => this;

        /// <summary>
        /// Initializes a new instance of the <see cref="RateLimitPolicy" /> class.
        /// </summary>
        /// <param name="limit">The limit.</param>
        /// <param name="unit">The unit.</param>
        /// <param name="whiteListedRequestKeysCommaSeparated">The white listed request keys comma separated without spaces.</param>
        /// <exception cref="System.ArgumentNullException">if whiteListedRequestKeys is null</exception>
        public RateLimitPolicy(int limit, RateLimitUnit unit, string whiteListedRequestKeysCommaSeparated)
        {
            if (limit <= 0) throw new ArgumentOutOfRangeException($"{nameof(limit)} has to be greater than 0");
            if (whiteListedRequestKeysCommaSeparated == null) throw new ArgumentNullException();

            Limit = limit;
            Unit = unit;
            WhiteListRequestKeys = whiteListedRequestKeysCommaSeparated.Split(',');
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

        /// <summary>
        /// Gets the white listed request keys.
        /// </summary>
        /// <value>
        /// The white listed request keys.
        /// </value>
        public IEnumerable<string> WhiteListRequestKeys { get;  }

        public override string ToString()
        {
            return $"{Limit} calls {Unit}";
        }
    }
}