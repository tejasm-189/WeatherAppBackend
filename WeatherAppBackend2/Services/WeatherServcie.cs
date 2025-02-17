//using System.Net.Http;
//using System.Net.Http.Json;
//using System.Threading.Tasks;
//using static System.Runtime.InteropServices.JavaScript.JSType;
//using Supabase.Gotrue;

//public class WeatherService
//{
//    private readonly HttpClient _httpClient;
//    private const string ApiKey = "7ff4e8541623bb019a138d175445d03a";
//    private const string BaseUrl = "https://api.openweathermap.org/data/2.5/forecast";

//    public WeatherService(HttpClient httpClient)
//    {
//        _httpClient = httpClient;
//    }
//    public async Task<object?> GetWeatherAsync(string city)
//    {
//        var url = $"{BaseUrl}?q={Uri.EscapeDataString(city)}&appid={ApiKey}";
//        Console.WriteLine($"Requesting: {url}");

//        return await _httpClient.GetFromJsonAsync<object>(url);
//    }

//}

using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

public class WeatherService
{
    private readonly HttpClient _httpClient;
    private readonly string _openWeatherApiKey;
    private readonly string _openWeatherBaseUrl;



    public WeatherService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;

        _openWeatherApiKey = config["OpenWeatherAPI:Key"]; // Store API Key in appsettings.json
        _openWeatherBaseUrl = config["OpenWeatherAPI:BaseUrl"];// Store Url in the appsettings.json

    }
    public async Task<string> IsValidCity(string city)
    {
        try
        {
            string apiUrl = $"{_openWeatherBaseUrl}?q={Uri.EscapeDataString(city)}&appid={_openWeatherApiKey}";
            var response = await _httpClient.GetAsync(apiUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content; // Return the response content as a string
            }
            else
            {
                // Return the error response content if the request was not successful
                var errorContent = await response.Content.ReadAsStringAsync();
                return errorContent;
            }
        }
        catch (HttpRequestException ex)
        {
            // Log exception details here if needed
            return $"HTTP Request Error: {ex.Message}";
        }
        catch (Exception ex)
        {
            // Log exception details here if needed
            return $"Error: {ex.Message}";
        }
    }
}




