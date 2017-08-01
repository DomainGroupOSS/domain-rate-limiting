using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.RateLimiting.Core;
using Domain.RateLimiting.Redis;

namespace Domain.RateLimiting.Samples.ConsoleCore
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IRateLimitingCacheProvider rateLimiter =
                new RedisSlidingWindowRateLimiter("localhost");
            
            var policies = new List<RateLimitPolicy>() {
                new RateLimitPolicy("test_client_throttle_1",
                    new List<AllowedCallRate>() {
                        new AllowedCallRate(2, RateLimitUnit.PerSecond)
                    }),
                new RateLimitPolicy("test_client_throttle_2", 
                    new List<AllowedCallRate>() {
                        new AllowedCallRate(2, RateLimitUnit.PerSecond),
                        new AllowedCallRate(10, RateLimitUnit.PerMinute)
                    }),
                new RateLimitPolicy("test_client_throttle_3",
                    new List<AllowedCallRate>() {
                        new AllowedCallRate(3, RateLimitUnit.PerSecond),
                        new AllowedCallRate(10, RateLimitUnit.PerMinute)
                    }),
                new RateLimitPolicy("test_client_throttle_4",
                    new List<AllowedCallRate>() {
                        new AllowedCallRate(3, RateLimitUnit.PerSecond),
                        new AllowedCallRate(10, RateLimitUnit.PerMinute)
                    })
                };

            const int numberOfThreads = 10;

            Console.WriteLine("Using RedisSlidingWindowRateLimiter");
            Console.WriteLine("");

            Console.WriteLine($"Running limiter with a policy of 2 allowed call rates (2 perSecond and 10 perMinute)  with 10 Threads each making 100 requests");
            var taskArray = new Task[numberOfThreads];

            Parallel.ForEach<int>(Enumerable.Range(0, numberOfThreads).ToArray(), (_) =>
            {
                PrintAverageTimeInMilliseconds(new
                {
                    TotalNumberOfCalls = 100,
                    RateLimiter = rateLimiter,
                    Policy = new RateLimitPolicy("test_client_perf",
                        new List<AllowedCallRate>() {
                            new AllowedCallRate(2, RateLimitUnit.PerMinute)
                        })
                });
            });

            Console.WriteLine("");

            Console.WriteLine("Starting throttle count verifications");

            Console.WriteLine("");

            Console.WriteLine("Starting throttle count verification for 100 requests with a policy (2 perSecond) with a delay of 100 milliseconds");
            Console.WriteLine($"Excepted ThrottleCount around 80 and Actual Throttle Count = " +
                              $"{GetThrottleCount(rateLimiter, policies[0], 100, 100)}");

            Console.WriteLine("");
            Console.WriteLine("Starting throttle count verification for 100 requests with a policy of 2 allowed call rates (2 perSecond and 10 perMinute) with a delay of 2 milliseconds");
            Console.WriteLine($"Excepted ThrottleCount around 90 and Actual ThrottleCount = {GetThrottleCount(rateLimiter, policies[1], 100, 100)}");

            Console.WriteLine("");
            Console.WriteLine("Starting throttle count verification for 100 requests with a policy of 2 allowed call rates (3 perSecond and 10 perMinute) with a delay of 2 milliseconds");
            Console.WriteLine($"Excepted ThrottleCount around 97 and Actual ThrottleCount = " +
                              $"Count {GetThrottleCount(rateLimiter, policies[2], 100, 2)}");

            Console.WriteLine("");
            Console.WriteLine("Starting throttle count verification for 100 requests with a policy of 2 allowed call rates (3 perSecond and 10 perMinute) with a delay of 100 milliseconds");
            Console.WriteLine($"Excepted ThrottleCount around 90 and Actual ThrottleCount = " +
                              $"Count {GetThrottleCount(rateLimiter, policies[3], 100, 100)}");
        }

        private static int GetThrottleCount(IRateLimitingCacheProvider rateLimiter,
            RateLimitPolicy policy,
            int totalNumberOfCalls,
            int sleepTimeInMilliSeconds)
        {
            int throttleCount = 0;
            for (var i = 1; i <= totalNumberOfCalls; i++)
            {
                if (rateLimiter.LimitRequestAsync(policy.RequestKey, policy.HttpMethod,
                    "TestRateLimiting.com", policy.RouteTemplate, policy.AllowedCallRates).Result.Throttled)
                {
                    throttleCount++;
                }

                Thread.Sleep(sleepTimeInMilliSeconds);
            }
            return throttleCount;
        }

        private static void PrintAverageTimeInMilliseconds(dynamic parameters)
        {
            double totalTime = 0;
            for (var i = 1; i <= parameters.TotalNumberOfCalls; i++)
            {
                var then = DateTime.Now;
                var throttled = parameters.RateLimiter.LimitRequestAsync(parameters.Policy.RequestKey, parameters.Policy.HttpMethod,
                    "TestRateLimiting.com", parameters.Policy.RouteTemplate, parameters.Policy.AllowedCallRates).Result;

                totalTime += DateTime.Now.Subtract(then).TotalMilliseconds;
            }

            Console.WriteLine($"Average time in milliseconds for {parameters.TotalNumberOfCalls} is " +
                              $"{totalTime / parameters.TotalNumberOfCalls} for thread { Thread.CurrentThread.ManagedThreadId}");
        }
    }
}
