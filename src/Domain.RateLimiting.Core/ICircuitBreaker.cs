using System;
using System.Threading.Tasks;

namespace Domain.RateLimiting.Core
{
    /// <summary>
    /// 
    /// </summary>
    public interface ICircuitBreaker
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="action"></param>
        /// <param name="defaultResult"></param>
        /// <returns></returns>
        Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> action, TResult defaultResult);

    }
}
