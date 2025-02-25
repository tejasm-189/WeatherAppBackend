using Microsoft.AspNetCore.Mvc;

[Route("api/weatherupdate")]
[ApiController]
public class WeatherUpdateController : ControllerBase
{
    private readonly FavoriteCitiesSyncService _syncService;

    public WeatherUpdateController(FavoriteCitiesSyncService syncService)
    {
        _syncService = syncService;
    }

    [HttpPost("daily-update")]
    public async Task<IActionResult> DailyUpdate()
    {
        try
        {
            await _syncService.SyncFavoriteCitiesAsync();
            return Ok("Table refreshed and emails sent!");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok("Weather update service is running.");
    }
}