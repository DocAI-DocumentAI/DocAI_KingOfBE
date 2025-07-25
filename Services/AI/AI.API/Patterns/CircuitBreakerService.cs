using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AI.API.Patterns
{
    /// <summary>
    /// Circuit breaker pattern implementation for external service calls
    /// Prevents cascading failures and provides fallback mechanisms
    /// </summary>
    public class CircuitBreakerService
    {
        private readonly CircuitBreakerOptions _options;
        private readonly ILogger<CircuitBreakerService> _logger;
        private readonly object _lock = new object();
        
        private CircuitState _state = CircuitState.Closed;
        private int _failureCount = 0;
        private DateTime _lastFailureTime = DateTime.MinValue;
        private DateTime _nextAttemptTime = DateTime.MinValue;

        public CircuitBreakerService(IOptions<CircuitBreakerOptions> options, ILogger<CircuitBreakerService> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        /// <summary>
        /// Executes an operation with circuit breaker protection
        /// </summary>
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string operationName, Func<Task<T>> fallback = null)
        {
            if (IsCircuitOpen())
            {
                _logger.LogWarning("Circuit breaker is OPEN for operation {OperationName}. Using fallback.", operationName);
                
                if (fallback != null)
                {
                    return await fallback();
                }
                
                throw new CircuitBreakerOpenException($"Circuit breaker is open for operation: {operationName}");
            }

            try
            {
                var result = await operation();
                OnSuccess(operationName);
                return result;
            }
            catch (Exception ex)
            {
                OnFailure(operationName, ex);
                
                if (fallback != null && IsCircuitOpen())
                {
                    _logger.LogInformation("Using fallback for failed operation {OperationName}", operationName);
                    return await fallback();
                }
                
                throw;
            }
        }

        /// <summary>
        /// Executes an operation with circuit breaker protection (void return)
        /// </summary>
        public async Task ExecuteAsync(Func<Task> operation, string operationName, Func<Task> fallback = null)
        {
            if (IsCircuitOpen())
            {
                _logger.LogWarning("Circuit breaker is OPEN for operation {OperationName}. Using fallback.", operationName);
                
                if (fallback != null)
                {
                    await fallback();
                    return;
                }
                
                throw new CircuitBreakerOpenException($"Circuit breaker is open for operation: {operationName}");
            }

            try
            {
                await operation();
                OnSuccess(operationName);
            }
            catch (Exception ex)
            {
                OnFailure(operationName, ex);
                
                if (fallback != null && IsCircuitOpen())
                {
                    _logger.LogInformation("Using fallback for failed operation {OperationName}", operationName);
                    await fallback();
                    return;
                }
                
                throw;
            }
        }

        /// <summary>
        /// Gets current circuit breaker status
        /// </summary>
        public CircuitBreakerStatus GetStatus()
        {
            lock (_lock)
            {
                return new CircuitBreakerStatus
                {
                    State = _state,
                    FailureCount = _failureCount,
                    LastFailureTime = _lastFailureTime,
                    NextAttemptTime = _nextAttemptTime
                };
            }
        }

        /// <summary>
        /// Manually resets the circuit breaker
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _state = CircuitState.Closed;
                _failureCount = 0;
                _lastFailureTime = DateTime.MinValue;
                _nextAttemptTime = DateTime.MinValue;
                
                _logger.LogInformation("Circuit breaker has been manually reset");
            }
        }

        private bool IsCircuitOpen()
        {
            lock (_lock)
            {
                if (_state == CircuitState.Open)
                {
                    if (DateTime.UtcNow >= _nextAttemptTime)
                    {
                        _state = CircuitState.HalfOpen;
                        _logger.LogInformation("Circuit breaker moved to HALF-OPEN state");
                        return false;
                    }
                    return true;
                }
                
                return false;
            }
        }

        private void OnSuccess(string operationName)
        {
            lock (_lock)
            {
                if (_state == CircuitState.HalfOpen)
                {
                    _state = CircuitState.Closed;
                    _failureCount = 0;
                    _logger.LogInformation("Circuit breaker CLOSED after successful operation {OperationName}", operationName);
                }
            }
        }

        private void OnFailure(string operationName, Exception exception)
        {
            lock (_lock)
            {
                _failureCount++;
                _lastFailureTime = DateTime.UtcNow;
                
                _logger.LogWarning(exception, "Operation {OperationName} failed. Failure count: {FailureCount}", 
                    operationName, _failureCount);

                if (_failureCount >= _options.FailureThreshold)
                {
                    _state = CircuitState.Open;
                    _nextAttemptTime = DateTime.UtcNow.Add(_options.OpenTimeout);
                    
                    _logger.LogError("Circuit breaker OPENED for operation {OperationName}. Next attempt at {NextAttemptTime}", 
                        operationName, _nextAttemptTime);
                }
            }
        }
    }

    /// <summary>
    /// Circuit breaker configuration options
    /// </summary>
    public class CircuitBreakerOptions
    {
        /// <summary>
        /// Number of failures before opening the circuit
        /// </summary>
        public int FailureThreshold { get; set; } = 5;

        /// <summary>
        /// Time to wait before attempting to close the circuit
        /// </summary>
        public TimeSpan OpenTimeout { get; set; } = TimeSpan.FromMinutes(1);
    }

    /// <summary>
    /// Circuit breaker states
    /// </summary>
    public enum CircuitState
    {
        Closed,   // Normal operation
        Open,     // Circuit is open, requests fail fast
        HalfOpen  // Testing if service is back online
    }

    /// <summary>
    /// Circuit breaker status information
    /// </summary>
    public class CircuitBreakerStatus
    {
        public CircuitState State { get; set; }
        public int FailureCount { get; set; }
        public DateTime LastFailureTime { get; set; }
        public DateTime NextAttemptTime { get; set; }
    }

    /// <summary>
    /// Exception thrown when circuit breaker is open
    /// </summary>
    public class CircuitBreakerOpenException : Exception
    {
        public CircuitBreakerOpenException(string message) : base(message) { }
        public CircuitBreakerOpenException(string message, Exception innerException) : base(message, innerException) { }
    }
}
