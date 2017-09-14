using System;
using System.Threading;
using System.Threading.Tasks;

namespace Domain.RateLimiting.Core
{
    public class DefaultCircuitBreaker : ICircuitBreaker
    {
        private readonly int _faultThreshholdPerWindowDuration;
        private readonly int _faultWindowDurationInMilliseconds;
        private readonly int _circuitOpenIntervalInSecs;

        private int _consecutiveFaultsCount;
        private DateTime _faultWindowOpenTime;


        private readonly SemaphoreSlim _circuitLock = new SemaphoreSlim(1);

        private DateTime _circuitOpenTime;
        private bool _circuitIsOpen;
        private readonly Action _onCircuitOpened;
        private readonly Action _onCircuitClosed;
        private readonly Action<Exception> _onCircuitException;

        public DefaultCircuitBreaker(int faultThreshholdPerWindowDuration,
            int faultWindowDurationInMilliseconds,
            int circuitOpenIntervalInSecs,
            Action onCircuitOpened = null,
            Action onCircuitClosed = null,
            Action<Exception> onCircuitException = null)
        {
            _onCircuitClosed = onCircuitClosed;
            _onCircuitOpened = onCircuitOpened;
            _onCircuitException = onCircuitException;
            _faultThreshholdPerWindowDuration = faultThreshholdPerWindowDuration;
            _faultWindowDurationInMilliseconds = faultWindowDurationInMilliseconds;
            _circuitOpenIntervalInSecs = circuitOpenIntervalInSecs;
        }

        public async Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> action, TResult defaultResult)
        {
            if (_circuitIsOpen)
            {
                if (DateTime.Now.Subtract(_circuitOpenTime).TotalSeconds <= _circuitOpenIntervalInSecs)
                {
                    return defaultResult;
                }

                await _circuitLock.WaitAsync();

                if (_circuitIsOpen)
                {
                    _consecutiveFaultsCount = 0;
                    _circuitIsOpen = false;
                    _onCircuitClosed?.Invoke();
                }

                _circuitLock.Release();
            }

            try
            {
                return await action.Invoke().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _onCircuitException?.Invoke(ex);

                if (_circuitIsOpen)
                    return defaultResult;

                await _circuitLock.WaitAsync();

                if (_circuitIsOpen)
                    return defaultResult;

                if (_consecutiveFaultsCount == 0)
                    _faultWindowOpenTime = DateTime.Now;

                _consecutiveFaultsCount++;
                if (_consecutiveFaultsCount <= _faultThreshholdPerWindowDuration)
                    return defaultResult;

                if (DateTime.Now.Subtract(_faultWindowOpenTime).TotalMilliseconds <= _faultWindowDurationInMilliseconds)
                {
                    _circuitOpenTime = DateTime.Now;
                    _circuitIsOpen = true;
                    _onCircuitOpened?.Invoke();
                }
                else
                {
                    _consecutiveFaultsCount = 1;
                    _faultWindowOpenTime = DateTime.Now;
                }

                _circuitLock.Release();
            }

            return defaultResult;
        }
    }
}
