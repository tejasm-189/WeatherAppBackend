using Supabase;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add HttpClient
builder.Services.AddHttpClient();

// Register Supabase Client as a singleton with DI
builder.Services.AddSingleton(sp =>
{
    var url = Environment.GetEnvironmentVariable("SUPABASE_URL") ?? builder.Configuration["Supabase:Url"];
    var serviceRoleKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY") ?? builder.Configuration["Supabase:ServiceRoleKey"];
    var options = new Supabase.SupabaseOptions { AutoConnectRealtime = false }; // Realtime not needed here
    return new Supabase.Client(url, serviceRoleKey, options);
});

// Register custom services
builder.Services.AddScoped<WeatherService>(sp =>
{
    var weatherSettings = builder.Configuration.GetSection("OpenWeatherAPI");
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new WeatherService(
        httpClient,
        weatherSettings["Key"] ?? Environment.GetEnvironmentVariable("KEY"),
        weatherSettings["BaseUrl"] ?? Environment.GetEnvironmentVariable("BASE_URL"));
});

builder.Services.AddScoped<MongoService>(sp =>
{
    var mongoSettings = builder.Configuration.GetSection("MongoDB");
    return new MongoService(
        mongoSettings["ConnectionString"] ?? Environment.GetEnvironmentVariable("CONNECTION_STRING"),
        mongoSettings["Database"] ?? Environment.GetEnvironmentVariable("DATABASE_NAME"),
        mongoSettings["UserCollectionName"] ?? Environment.GetEnvironmentVariable("COLLECTIONSTRING"));
});

builder.Services.AddScoped<FavoriteCitiesSyncService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


// Initialize Supabase (single instance)
var supabase = app.Services.GetRequiredService<Supabase.Client>();
await supabase.InitializeAsync();


// Call SyncFavoriteCitiesAsync on startup
using (var scope = app.Services.CreateScope())
{
    var syncService = scope.ServiceProvider.GetRequiredService<FavoriteCitiesSyncService>();
    await syncService.SyncFavoriteCitiesAsync();
}



app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();

