using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Castle.DynamicProxy.Generators;
using Domain.RateLimiting.Core;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Domain.RateLimiting.Redis.UnitTests
{
    public class RedisSlidingWindowRateLimiterTests
    {
        private static (RedisRateLimiter RedisRateLimiter, 
            Mock<IConnectionMultiplexer> ConnectionMultiplexerMock,
            Mock<IDatabase> DatabaseMock,
            Mock<ITransaction> TransactionMock,
            Mock<ITransaction> PostViolationTransactionMock,
            Mock<IClock> clockMock) 
            Arrange(string requestId, string method, string routeTemplate,
            AllowedCallRate allowedCallRate,
            DateTime utcDateTime, long numberOfRequestsMadeSoFar)
        {
            var clockMock = GetClockMock(utcDateTime);

            var cacheKey = new RateLimitCacheKey(requestId, method, "localhost", routeTemplate,
                allowedCallRate,
                _ => allowedCallRate.Unit.ToString(), clockMock.Object);

            var connectionMultiplexerMock = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);

            var dbMock = new Mock<IDatabase>(MockBehavior.Strict);

            var transactionMock = new Mock<ITransaction>(MockBehavior.Strict);
            
            transactionMock.Setup(redisTransaction =>
                redisTransaction.SortedSetRemoveRangeByScoreAsync(
                    cacheKey.ToString(), 0,
                    utcDateTime.Ticks - (long)cacheKey.Unit, It.IsAny<Exclude>(),
                    It.IsAny<CommandFlags>())).Returns(Task.FromResult(10L));

            transactionMock.Setup(redisTransaction =>
                redisTransaction.SortedSetAddAsync(
                    cacheKey.ToString(), It.IsAny<RedisValue>(), utcDateTime.Ticks, It.IsAny<When>(), 
                    It.IsAny<CommandFlags>())).Returns(Task.FromResult(true));

            transactionMock.Setup(redisTransaction =>
                redisTransaction.SortedSetLengthAsync(
                    cacheKey.ToString(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Exclude>(),
                    It.IsAny<CommandFlags>())).Returns(
                Task.FromResult(numberOfRequestsMadeSoFar));

            transactionMock.Setup(redisTransaction =>
                redisTransaction.KeyExpireAsync(
                    cacheKey.ToString(), cacheKey.Expiration.Add(new TimeSpan(0, 1, 0)), It.IsAny<CommandFlags>())).Returns(Task.FromResult(true));

            transactionMock.Setup(redisTransaction =>
                redisTransaction.ExecuteAsync(CommandFlags.None)).Returns(Task.FromResult(true));

            var postViolationTransactionMock = new Mock<ITransaction>(MockBehavior.Strict);

            postViolationTransactionMock.Setup(redisTransaction =>
                redisTransaction.SortedSetRangeByRankWithScoresAsync(
                    cacheKey.ToString(), 0, 0, It.IsAny<Order>(),
                    It.IsAny<CommandFlags>()))
                    .Returns(Task.FromResult(new SortedSetEntry[] { new SortedSetEntry(It.IsAny<RedisValue>(), utcDateTime.Ticks)}));
            
            postViolationTransactionMock.Setup(redisTransaction =>
                redisTransaction.ExecuteAsync(CommandFlags.None)).Returns(Task.FromResult(true));

            dbMock.SetupSequence(db => db.CreateTransaction(null))
                .Returns(transactionMock.Object)
                .Returns(postViolationTransactionMock.Object);

            connectionMultiplexerMock.Setup(connection => connection.IsConnected).Returns(true);
            connectionMultiplexerMock.Setup(connection => connection.GetDatabase(-1, null)).Returns(dbMock.Object);

            var rateLimiter = new RedisSlidingWindowRateLimiter("http://localhost",
                clock: clockMock.Object,
                connectToRedisFunc: async () => await Task.FromResult(connectionMultiplexerMock.Object), 
                countThrottledRequests: true);

            return (rateLimiter, connectionMultiplexerMock, dbMock, 
                transactionMock, postViolationTransactionMock, clockMock);

        }

        [Fact]
        public async void ShouldNotThrottleOnFirstCallWithAllowedCallRateOf2PerMinute()
        {
            var utcDateTime = DateTime.UtcNow;

            var clockMock = GetClockMock(utcDateTime);

            var cacheKey = new RateLimitCacheKey("testclient_01", "GET", "localhost", "/api/values", 
                new AllowedCallRate(2, RateLimitUnit.PerMinute),
                _ => RateLimitUnit.PerMinute.ToString(), clockMock.Object);

            var setup = Arrange("testclient_01", "GET", "/api/values", new AllowedCallRate(2, RateLimitUnit.PerMinute),
                utcDateTime, 1);

            var result = await setup.RedisRateLimiter.LimitRequestAsync("testclient_01",
                "GET", "localhost", "/api/values", new List<AllowedCallRate>()
                {
                    new AllowedCallRate(2, RateLimitUnit.PerMinute)
                }, 1).ConfigureAwait(false);

            Assert.Equal(false, result.Throttled);
            Assert.Equal(0, result.WaitingIntervalInTicks);
            Assert.Equal(1, result.TokensRemaining);
            Assert.Equal(cacheKey.ToString(), result.CacheKey.ToString());

            setup.ConnectionMultiplexerMock.VerifyAll();
            setup.DatabaseMock.VerifyAll();
            setup.TransactionMock.VerifyAll();
            setup.PostViolationTransactionMock.Verify(redisTransaction =>
                    redisTransaction.SortedSetRangeByRankWithScoresAsync(
                        cacheKey.ToString(), 0, 0, It.IsAny<Order>(),
                        It.IsAny<CommandFlags>()),Times.Never);

            setup.clockMock.VerifyAll();
        }

        private static Mock<IClock> GetClockMock(DateTime utcDateTime)
        {
            var clockMock = new Mock<IClock>(MockBehavior.Strict);
            clockMock.Setup(clock => clock.GetCurrentUtcTimeInTicks()).Returns(utcDateTime.Ticks);
            clockMock.Setup(clock => clock.GetUtcDateTime()).Returns(utcDateTime);
            return clockMock;
        }

        [Fact]
        public async void ShouldNotThrottleOnSecondCallWithAllowedCallRateOf2PerMinute()
        {
            var utcDateTime = DateTime.UtcNow;

            var clockMock = GetClockMock(utcDateTime);

            var cacheKey = new RateLimitCacheKey("testclient_01", "GET", "localhost", "/api/values",
                new AllowedCallRate(2, RateLimitUnit.PerMinute),
                _ => RateLimitUnit.PerMinute.ToString(), clockMock.Object);

            var setup = Arrange("testclient_01", "GET", "/api/values", new AllowedCallRate(2, RateLimitUnit.PerMinute),
                utcDateTime, 2);

            var result = await setup.RedisRateLimiter.LimitRequestAsync("testclient_01",
                "GET", "localhost", "/api/values", new List<AllowedCallRate>()
                {
                    new AllowedCallRate(2, RateLimitUnit.PerMinute)
                }, 1).ConfigureAwait(false);

            Assert.Equal(false, result.Throttled);
            Assert.Equal(0, result.WaitingIntervalInTicks);
            Assert.Equal(0, result.TokensRemaining);
            Assert.Equal(cacheKey.ToString(), result.CacheKey.ToString());

            setup.ConnectionMultiplexerMock.VerifyAll();
            setup.DatabaseMock.VerifyAll();
            setup.TransactionMock.VerifyAll();
            setup.PostViolationTransactionMock.Verify(redisTransaction =>
                redisTransaction.SortedSetRangeByRankWithScoresAsync(
                    cacheKey.ToString(), 0, 0, It.IsAny<Order>(),
                    It.IsAny<CommandFlags>()), Times.Never);

            setup.clockMock.VerifyAll();
        }

        [Fact]
        public async void ShouldThrottleOnThirdCallWithAllowedCallRateOf2PerMinute()
        {
            var utcDateTime = DateTime.UtcNow;

            var clockMock = GetClockMock(utcDateTime);

            var cacheKey = new RateLimitCacheKey("testclient_01", "GET", "localhost", "/api/values",
                new AllowedCallRate(2, RateLimitUnit.PerMinute),
                _ => RateLimitUnit.PerMinute.ToString(), clockMock.Object);

            var setup = Arrange("testclient_01", "GET", "/api/values", new AllowedCallRate(2, RateLimitUnit.PerMinute),
                utcDateTime, 3);

            var result = await setup.RedisRateLimiter.LimitRequestAsync("testclient_01",
                "GET", "localhost", "/api/values", new List<AllowedCallRate>()
                {
                    new AllowedCallRate(2, RateLimitUnit.PerMinute)
                }).ConfigureAwait(false);

            Assert.Equal(true, result.Throttled);
            Assert.Equal(cacheKey.ToString(), result.CacheKey.ToString());

            setup.ConnectionMultiplexerMock.VerifyAll();
            setup.DatabaseMock.VerifyAll();
            setup.TransactionMock.VerifyAll();
            setup.PostViolationTransactionMock.VerifyAll();
            setup.clockMock.VerifyAll();
        }
    }
}
