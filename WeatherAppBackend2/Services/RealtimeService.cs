using Supabase.Realtime;

namespace WeatherAppBackend2.Services
{
    public class RealtimeService : BackgroundService
    {
        private readonly Supabase.Client _supabase;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, DateTime> _lastEmailSent = new();
        private readonly TimeSpan _debouncePeriod = TimeSpan.FromMinutes(1);

        public RealtimeService(Supabase.Client supabase, IHttpClientFactory httpClientFactory)
        {
            _supabase = supabase;
            _httpClient = httpClientFactory.CreateClient();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var channel = _supabase
                .From("user_favorite_cities_weather")
                .On(RealtimeChannel.EventType.Update, async (sender, change) =>
                {
                    var updatedRecord = change.Payload.New as Dictionary<string, object>;
                    if (updatedRecord != null && updatedRecord.TryGetValue("user_id", out var userIdObj))
                    {
                        var userId = userIdObj.ToString();
                        await SendEmailForUser(userId);
                    }
                });

            await channel.Subscribe(stoppingToken);

            // Keep the service running until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task SendEmailForUser(string userId)
        {
            lock (_lastEmailSent)
            {
                if (_lastEmailSent.TryGetValue(userId, out var lastSent) &&
                    DateTime.UtcNow - lastSent < _debouncePeriod)
                    return;
                _lastEmailSent[userId] = DateTime.UtcNow;
            }

            var userResponse = await _supabase.Auth.Admin.GetUserById(userId);
            if (userResponse?.User == null) return;

            var citiesResponse = await _supabase
                .From("user_favorite_cities_weather")
                .Filter("user_id", "eq", userId)
                .Get();

            var emailBody = "Today's Weather:\n" + string.Join("\n", citiesResponse.Models.Select(c =>
                $"{c["city_name"]}: Max {c["max_temp"]}°C, Min {c["min_temp"]}°C"));
            await SendEmail(userResponse.User.Email, "Daily Weather Update", emailBody);
        }

        private async Task SendEmail(string to, string subject, string body)
        {
            // Placeholder: Implement with MailKit or SendGrid
            Console.WriteLine($"Sending email to {to}: {subject} - {body}");
            await Task.CompletedTask;
        }
    }
}
