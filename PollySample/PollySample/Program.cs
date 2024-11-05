using System.Net;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using PollySample;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("RetrySample")
    .AddResilienceHandler
    (
        "RetrySamplePipeline",
        (pipelineBuilder,context)=>
        {
            // Retry
            var loggerFactory = context.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("SamplePipeline-Retry");
            var retryStrategyOptions = new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                                .HandleResult
                                (
                                    response => response.StatusCode is not HttpStatusCode.OK 
                                ),
                Delay = TimeSpan.FromSeconds(1),
                MaxRetryAttempts = 3,
                UseJitter = true,
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = (args) =>
                {
                    logger.LogInformation
                    (
                        "Retry Attempt Number : {AttemptNumber} after {TotalSeconds} seconds.",
                        args.AttemptNumber,
                        args.RetryDelay.TotalSeconds
                    );
                    return ValueTask.CompletedTask;
                },   
            };
            
            pipelineBuilder.AddRetry(retryStrategyOptions);                        
        }
    );

// CircuitBreakerSample

builder.Services.AddKeyedSingleton<CircuitBreakerStateProvider>(CircuitBreakerPipelineNames.Sample);
builder.Services.AddKeyedSingleton<CircuitBreakerManualControl>(CircuitBreakerPipelineNames.Sample);

builder.Services.AddHttpClient("CircuitBreakerSample")
    .AddResilienceHandler
    (
        CircuitBreakerPipelineNames.Sample,
        (pipelineBuilder,context)=>
        {
            // CircuitBreaker
            var loggerFactory = context.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("SamplePipeline-CircuitBreaker");
            
            var stateProvider = context.ServiceProvider.GetRequiredKeyedService<CircuitBreakerStateProvider>(CircuitBreakerPipelineNames.Sample);
            var manualControl = context.ServiceProvider.GetRequiredKeyedService<CircuitBreakerManualControl>(CircuitBreakerPipelineNames.Sample);
            
            var circuitBreakerStrategyOptions = new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.01,
                SamplingDuration = TimeSpan.FromMinutes(1),
                MinimumThroughput = 2,
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult
                    (
                        response => response.StatusCode is HttpStatusCode.OK
                    ),
                StateProvider = stateProvider,
                ManualControl = manualControl,
                OnOpened = arg =>
                {
                    logger.LogInformation("Circuit opened for {Duration} seconds due to: {StatusCode}", arg.BreakDuration.TotalSeconds, arg.Outcome.Result?.StatusCode);
                    return ValueTask.CompletedTask;
                },
                OnClosed = arg =>
                {
                    logger.LogInformation("Circuit closed. Resuming normal operation.");
                    return ValueTask.CompletedTask;
                }
            };
            pipelineBuilder.AddCircuitBreaker(circuitBreakerStrategyOptions);                        
        }
    );

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();