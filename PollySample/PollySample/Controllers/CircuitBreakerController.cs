using Microsoft.AspNetCore.Mvc;
using Polly.CircuitBreaker;

namespace PollySample.Controllers;

public class CircuitBreakerController : Controller
{
    private readonly CircuitBreakerStateProvider _circuitBreakerStateProvider;
    
    private readonly CircuitBreakerManualControl _breakerManualControl;

    public CircuitBreakerController([FromKeyedServices(CircuitBreakerPipelineNames.Sample)]CircuitBreakerStateProvider circuitBreakerStateProvider, 
                                    [FromKeyedServices(CircuitBreakerPipelineNames.Sample)]CircuitBreakerManualControl breakerManualControl)
    {
        _circuitBreakerStateProvider = circuitBreakerStateProvider;
        _breakerManualControl = breakerManualControl;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var circuitState = _circuitBreakerStateProvider.CircuitState.ToString();
        ViewData["CircuitState"] = circuitState;
        return View();
    }

    [HttpPost("open")]
    public async Task<IActionResult> OpenAsync()
    {
        await _breakerManualControl.IsolateAsync();
        return RedirectToAction("Index");
    }

    [HttpPost("close")]
    public async Task<IActionResult> CloseAsync()
    {
        await _breakerManualControl.CloseAsync();
        return RedirectToAction("Index");
    }
}
