using Microsoft.AspNetCore.Mvc;

namespace PollySample.Controllers;

public class TestController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;

    public TestController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public async Task<IActionResult> CircuitBreaker()
    {
        var httpClient = _httpClientFactory.CreateClient("CircuitBreakerSample");

        var response = await httpClient.GetAsync("https://ian26875.github.io/");

        return Content(response.StatusCode.ToString());
    }
    
    [HttpGet]
    public async Task<IActionResult> Retry()
    {
        var httpClient = _httpClientFactory.CreateClient("RetrySample");

        var response = await httpClient.GetAsync($"https://ian26875.github.io/Home/Error/");

        return Content(response.StatusCode.ToString());
    }
}