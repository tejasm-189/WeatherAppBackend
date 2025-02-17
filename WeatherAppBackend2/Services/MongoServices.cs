using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.Extensions.Options;

public class MongoService
{
    private readonly IMongoCollection<FavoriteCity> _favoritesCollection;

    public MongoService(IConfiguration config)
    {
        var client = new MongoClient(config["MongoDB:ConnectionString"]);
        var database = client.GetDatabase(config["MongoDB:Database"]);
        _favoritesCollection = database.GetCollection<FavoriteCity>(config["MongoDB:CollectionName"]);
    }

    public async Task<List<FavoriteCity>> GetFavorites(string userId)
    {
        return await _favoritesCollection.Find(f => f.UserId == userId).ToListAsync();
    }

    public async Task<string> AddFavorite(FavoriteCity favorite)
    {
        try { 
        var existingFavorite = await _favoritesCollection.Find(f => f.UserId == favorite.UserId && f.City == favorite.City).FirstOrDefaultAsync();
       
            if (existingFavorite == null)
            {
                await _favoritesCollection.InsertOneAsync(favorite);
                Console.WriteLine("Added");

                return $"Added";
            }
            else {
                Console.WriteLine("Was not Added");
                return "City Already Exists ";
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            string res = $"Error{e}";
            return res;    
        }
        }

    public async Task RemoveFavorite(string userId, string city)
    {
        var filter = Builders<FavoriteCity>.Filter.And(
            Builders<FavoriteCity>.Filter.Eq(f => f.UserId, userId),
            Builders<FavoriteCity>.Filter.Eq(f => f.City, city)
        );
        await _favoritesCollection.DeleteOneAsync(filter);
    }
}

public class FavoriteCity
{
    public ObjectId Id { get; set; }
    public string UserId { get; set; }
    public string City { get; set; }
    
}
