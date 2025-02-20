var builder = WebApplication.CreateBuilder(args);

// Add HttpClient registration so that HttpClient can be injected.
builder.Services.AddHttpClient();

// Add services to the container.
builder.Services.AddScoped<WeatherService>(sp =>
{
    var weatherSettings = builder.Configuration.GetSection("OpenWeatherAPI");
    var httpClient = sp.GetRequiredService<HttpClient>();
    return new WeatherService(
        httpClient,
        weatherSettings["Key"] ?? Environment.GetEnvironmentVariable("KEY"),
        weatherSettings["BaseUrl"] ?? Environment.GetEnvironmentVariable("BASE_URL"));
});
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddScoped<MongoService>(sp =>
{
    var mongoSettings = builder.Configuration.GetSection("MongoDB");
    return new MongoService(
        mongoSettings["ConnectionString"] ?? Environment.GetEnvironmentVariable("CONNECTION_STRING"),
        mongoSettings["Database"] ?? Environment.GetEnvironmentVariable("DATABASE_NAME"),
        mongoSettings["UserCollectionName"] ?? Environment.GetEnvironmentVariable("COLLECTIONSTRING"));
});


var url = Environment.GetEnvironmentVariable("https://zvyiblbxzuftnvoqipir.supabase.co");
var key = Environment.GetEnvironmentVariable("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Inp2eWlibGJ4enVmdG52b3FpcGlyIiwicm9sZSI6ImFub24iLCJpYXQiOjE3Mzg4NDIzOTMsImV4cCI6MjA1NDQxODM5M30._9d4-EYC6y95nkqE2oFArnEm68OZVYFsYbVa1L2erx8");

var options = new Supabase.SupabaseOptions
{
    AutoConnectRealtime = true
};

var supabase = new Supabase.Client(url, key, options);
await supabase.InitializeAsync();



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseRouting();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
