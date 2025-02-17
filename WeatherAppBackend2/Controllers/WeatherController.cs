using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

[Route("api/weather")]
[ApiController]
public class WeatherController : ControllerBase
{
    private readonly WeatherService _weatherService;

    public WeatherController(WeatherService weatherService)
    {
        _weatherService = weatherService;
    }

    [HttpGet("{city}")]
    public async Task<IActionResult> GetWeather(string city)
    {
        var data = await _weatherService.IsValidCity(city);
        if (data == null) return NotFound("Weather data not found.");
        return Ok(data);
    }
}
