using Microsoft.Extensions.Configuration;
using Supabase;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Generic;
using System.Linq;
using System;
using MongoDB.Driver.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Supabase.Postgrest.Models;
using Supabase.Postgrest.Attributes;

public class FavoriteCitiesSyncService
{
    private readonly Supabase.Client _supabase;
    private readonly HttpClient _httpClient;
    private readonly WeatherService _weatherService;
    private readonly MongoService _mongoService;
    private readonly IConfiguration _configuration;

    public FavoriteCitiesSyncService(
        Supabase.Client supabase,
        IHttpClientFactory httpClientFactory,
        WeatherService weatherService,
        MongoService mongoService,
        IConfiguration configuration)
    {
        _supabase = supabase;
        _httpClient = httpClientFactory.CreateClient();
        _weatherService = weatherService;
        _mongoService = mongoService;
        _configuration = configuration;
    }

    public async Task SyncFavoriteCitiesAsync()
    {
        try
        {
            // Step 1: Fetch all users from Supabase Auth
            var serviceKey = _configuration["Supabase:ServiceRoleKey"];
            var supabaseUrl = _configuration["Supabase:Url"] ?? "https://zvyiblbxzuftnvoqipir.supabase.co";

            // Initialize the Supabase client
            var client = new Supabase.Client(supabaseUrl, serviceKey);
            await client.InitializeAsync();

            // Use the User model to fetch all users
            var usersResponse = await client.From<User>().Get();

            // Step 3: Process each user
            if (usersResponse != null)
            {
                foreach (var user in usersResponse.Models) // Access the Models property
                {
                    // Fetch favorite cities from MongoDB via MongoService
                    var favoriteCities = await _mongoService.GetFavorites(user.userid);
                    if (favoriteCities == null || !favoriteCities.Any()) continue;

                    // Fetch weather data for all cities
                    foreach (var city in favoriteCities)
                    {
                        var weather = await FetchWeatherData(city.City); // Fetch weather data for the city

                        if (weather != null)
                        {
                            // Check if the record already exists
                            var existingRecord = await _supabase
                                .From<WeatherRecord>()
                                .Filter("UserId", Supabase.Postgrest.Constants.Operator.Equals, user.userid)
                                .Filter("CityName", Supabase.Postgrest.Constants.Operator.Equals, city.City)
                                .Get();

                            if (existingRecord.Models.Any())
                            {
                                // Update the existing record
                                var recordToUpdate = existingRecord.Models.First();
                                recordToUpdate.MaxTemp = weather.MaxTemp;
                                recordToUpdate.MinTemp = weather.MinTemp;
                                recordToUpdate.UpdatedAt = DateTime.UtcNow;

                                try
                                {
                                    // Update the record
                                    await _supabase.From<WeatherRecord>().Update(recordToUpdate);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error updating record for {city.City}: {ex.Message}");
                                }
                            }
                            else
                            {
                                // Insert new record if it doesn't exist
                                try
                                {
                                    // Log the data being inserted
                                    Console.WriteLine($"Inserting new record for {city.City}: MaxTemp={weather.MaxTemp}, MinTemp={weather.MinTemp}");

                                    await _supabase.From<WeatherRecord>().Insert(new WeatherRecord
                                    {
                                        UserId = user.userid,
                                        CityName = city.City,
                                        MaxTemp = weather.MaxTemp,
                                        MinTemp = weather.MinTemp,
                                        UpdatedAt = DateTime.UtcNow
                                    });
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error inserting record for {city.City}: {ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"No weather data available for {city.City}");
                        }
                    }
                }
            }

            Console.WriteLine("SyncFavoriteCities completed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SyncFavoriteCities: {ex.Message}");
            throw;
        }
    }

    private async Task<CurrentWeather> FetchWeatherData(string cityName)
    {
        var apiKey = _configuration["OpenWeatherAPi:Key"];
        var baseurl = _configuration["OpenWeatherAPi:BaseUrl"];
        var apiUrl = $"{baseurl}?q={Uri.EscapeDataString(cityName)}&appid={apiKey}";

        using var httpClient = new HttpClient();

        var response = await httpClient.GetAsync(apiUrl);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Error fetching data for {cityName}: {response.StatusCode}");
            return null;
        }

        var weatherData = JsonSerializer.Deserialize<WeatherApiResponse>(responseBody);

        if (weatherData == null)
        {
            Console.WriteLine($"Deserialization failed for city: {cityName}");
            return null;
        }

        Console.WriteLine($"Deserialized Weather Data for {cityName}: {JsonSerializer.Serialize(weatherData)}");

        if (weatherData.List == null || !weatherData.List.Any())
        {
            Console.WriteLine($"No weather data available for city: {cityName}");
            return null;
        }

        // Calculate current local day based on city's timezone
        var utcNow = DateTimeOffset.UtcNow;
        var offset = TimeSpan.FromSeconds(weatherData.City.Timezone);
        var localNow = utcNow.ToOffset(offset);
        var startOfDayLocal = new DateTimeOffset(localNow.Date, offset);
        var startOfDayUtc = startOfDayLocal.UtcDateTime;
        var endOfDayUtc = startOfDayLocal.AddDays(1).UtcDateTime;

        // Filter forecasts for the current local day
        var todayForecasts = weatherData.List
            .Where(item =>
            {
                var forecastUtc = DateTimeOffset.FromUnixTimeSeconds(item.Dt).UtcDateTime;
                return forecastUtc >= startOfDayUtc && forecastUtc < endOfDayUtc;
            })
            .ToList();

        float maxTemp, minTemp;
        WeatherListItem referenceItem;

        if (todayForecasts.Any())
        {
            maxTemp = todayForecasts.Max(item => item.Main.TempMax);
            minTemp = todayForecasts.Min(item => item.Main.TempMin);
            referenceItem = todayForecasts.First(); // Use first item for other weather details
            Console.WriteLine($"Calculated daily temps for {cityName} - Max: {maxTemp}K, Min: {minTemp}K from {todayForecasts.Count} forecasts");
        }
        else
        {
            // Fallback to first forecast item if no data for today
            referenceItem = weatherData.List.First();
            maxTemp = referenceItem.Main.TempMax;
            minTemp = referenceItem.Main.TempMin;
            Console.WriteLine($"No forecasts for today in {cityName}, using first item - Max: {maxTemp}K, Min: {minTemp}K");
        }

        // Convert to Celsius
        var maxTempC = maxTemp - 273.15f;
        var minTempC = minTemp - 273.15f;

        return new CurrentWeather
        {
            CityName = weatherData.City.Name,
            CountryCode = weatherData.City.Country,
            MaxTemp = maxTempC,
            MinTemp = minTempC,
            WindSpeed = referenceItem.Wind.Speed,
            Description = referenceItem.Weather.FirstOrDefault()?.Description,
            WeatherIcon = referenceItem.Weather.FirstOrDefault()?.Icon,
            Visibility = referenceItem.Visibility,
            UpdatedAt = DateTime.UtcNow
        };
    }

    // Updated model classes with JSON mappings
    public class WeatherApiResponse
    {
        [JsonPropertyName("city")]
        public CityInfo City { get; set; }

        [JsonPropertyName("list")]
        public List<WeatherListItem> List { get; set; }
    }

    public class CityInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }

        [JsonPropertyName("timezone")]
        public int Timezone { get; set; }
    }

    public class WeatherListItem
    {
        [JsonPropertyName("dt")]
        public long Dt { get; set; }

        [JsonPropertyName("main")]
        public MainInfo Main { get; set; }

        [JsonPropertyName("wind")]
        public WindInfo Wind { get; set; }

        [JsonPropertyName("weather")]
        public List<WeatherInfo> Weather { get; set; }

        [JsonPropertyName("visibility")]
        public int Visibility { get; set; }
    }

    public class MainInfo
    {
        [JsonPropertyName("temp_max")]
        public float TempMax { get; set; }

        [JsonPropertyName("temp_min")]
        public float TempMin { get; set; }
    }

    public class WindInfo
    {
        [JsonPropertyName("speed")]
        public float Speed { get; set; }
    }

    public class WeatherInfo
    {
        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("icon")]
        public string Icon { get; set; }
    }

    public class CurrentWeather
    {
        public string? CityName { get; set; }
        public string? CountryCode { get; set; }
        public MainInfo Main { get; set; } // Note: Kept for compatibility, though not used in return
        public string? Description { get; set; }
        public float WindSpeed { get; set; }
        public string? WeatherIcon { get; set; }
        public int Visibility { get; set; }
        public float MaxTemp { get; set; }
        public float MinTemp { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // Existing classes retained unchanged
    public class User : BaseModel
    {
        public string? userid { get; set; }
        public string? email { get; set; }
    }

    [Table("user_favorite_cities_weather")]
    public class WeatherRecord : BaseModel
    {
        [PrimaryKey("Id", false)]
        public int Id { get; set; }

        [Column("UserId")]
        public string? UserId { get; set; }

        [Column("CityName")]
        public string? CityName { get; set; }

        [Column("MaxTemp")]
        public float? MaxTemp { get; set; }

        [Column("MinTemp")]
        public float? MinTemp { get; set; }

        [Column("UpdatedAt")]
        public DateTime UpdatedAt { get; set; }
    }
}