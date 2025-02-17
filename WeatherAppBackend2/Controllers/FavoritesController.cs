namespace WeatherAppBackend2.Controllers
{
    using Microsoft.AspNetCore.Mvc;

    [Route("api/favorites")]
    [ApiController]
    public class FavoritesController : ControllerBase
    {
        private readonly MongoService _mongoService;

        public FavoritesController(MongoService mongoService)
        {
            _mongoService = mongoService;
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetFavorites(string userId)
        {
            var favorites = await _mongoService.GetFavorites(userId);
            return Ok(favorites);
        }

        [HttpPost]
        public async Task<IActionResult> AddFavorite([FromBody] FavoriteCity favorite)
        {
            var res = await _mongoService.AddFavorite(favorite);
            return Ok(res) ;
        }

        [HttpDelete]
        public async Task<IActionResult> RemoveFavorite([FromBody] FavoriteCity favorite)
        {
            await _mongoService.RemoveFavorite(favorite.UserId, favorite.City);
            return Ok(new { message = "Removed from favorites!" });
        }
    }

}
