using System.Net;
using Polly;
using Polly.CircuitBreaker;

namespace PollySample;

public static class CircuitBreakerStrategies
{
    public static CircuitBreakerStrategyOptions<HttpResponseMessage> CircuitBreaker(IServiceProvider serviceProvider, string serviceKeyed)
    {
        var stateProvider = serviceProvider.GetRequiredKeyedService<CircuitBreakerStateProvider>(serviceKeyed);
        var manualControl = serviceProvider.GetRequiredKeyedService<CircuitBreakerManualControl>(serviceKeyed);

        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger($"SamplePipeline-CircuitBreaker:{serviceKeyed}");

        var sleepDurationKey = new ResiliencePropertyKey<TimeSpan>("BreakDuration");
        
        var circuitBreakerStrategyOptions = new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            FailureRatio = 0.01,
            SamplingDuration = TimeSpan.FromMinutes(1),
            MinimumThroughput = 2,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                                .HandleResult(response => response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests),
            StateProvider = stateProvider,
            ManualControl = manualControl,
            BreakDurationGenerator = breakDurationGeneratorArguments =>
            {
                var canGetValue =  breakDurationGeneratorArguments.Context.Properties.TryGetValue(sleepDurationKey, out TimeSpan delay);
                return ValueTask.FromResult(canGetValue ? delay : TimeSpan.FromSeconds(30));
            },
            OnOpened = onCircuitOpenedArguments =>
            {
                var retryAfter = ExtractRetryAfterDelay(onCircuitOpenedArguments.Outcome.Result);
                if (retryAfter.HasValue)
                {
                    onCircuitOpenedArguments.Context.Properties.Set(sleepDurationKey,retryAfter.Value);
                    logger.LogInformation("Circuit opened with dynamic break duration due to Retry-After header: {Duration} seconds", retryAfter.Value.TotalSeconds);
                }
                else
                {
                    logger.LogInformation("Circuit opened for {Duration} seconds due to: {StatusCode}", onCircuitOpenedArguments.BreakDuration.TotalSeconds, onCircuitOpenedArguments.Outcome.Result?.StatusCode);
                }
                return ValueTask.CompletedTask;
            },
            OnClosed = onCircuitClosedArguments =>
            {
                logger.LogInformation("Circuit closed. Resuming normal operation.");
                return ValueTask.CompletedTask;
            }
        };

        return circuitBreakerStrategyOptions;
    }
    
    /// <summary>
    /// 從 HTTP 回應中解析 Retry-After 標頭，以獲取重試延遲時間。
    /// </summary>
    /// <param name="response">HTTP 回應。</param>
    /// <returns>Retry-After 指定的重試延遲時間，若無則返回 null。</returns>
    private static TimeSpan? ExtractRetryAfterDelay(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter is null)
        {
            return default(TimeSpan?);
        }

        if (response.Headers.RetryAfter.Date.HasValue)
        {
            var retryAfterDate = response.Headers.RetryAfter.Date.Value;
            return retryAfterDate - DateTimeOffset.UtcNow;
        }
       
        if (response.Headers.RetryAfter.Delta.HasValue)
        {
            return response.Headers.RetryAfter.Delta.Value;
        }

        return default(TimeSpan?);
    }
}
