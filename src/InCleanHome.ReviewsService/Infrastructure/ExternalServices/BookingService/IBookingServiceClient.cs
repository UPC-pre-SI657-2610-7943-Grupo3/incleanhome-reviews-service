using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace InCleanHome.ReviewsService.Infrastructure.ExternalServices.BookingService;

public interface IBookingServiceClient
{
    Task<BookingSummary?> GetBookingAsync(int bookingId, string bearerToken);
}

public record BookingSummary(int Id, int ClientId, int WorkerId, string ClientName, string Status);

public class BookingServiceClient(
    HttpClient http,
    IConfiguration configuration,
    ILogger<BookingServiceClient> logger) : IBookingServiceClient
{
    private string BaseUrl => configuration["Dependencies:BookingServiceUrl"] ?? "http://booking-service:5003";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BookingSummary?> GetBookingAsync(int bookingId, string bearerToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/v1/bookings/{bookingId}/receipt");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            using var resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
            return new BookingSummary(
                json.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
                json.TryGetProperty("clientId", out var c) ? c.GetInt32() : 0,
                json.TryGetProperty("workerId", out var w) ? w.GetInt32() : 0,
                json.TryGetProperty("clientName", out var cn) ? cn.GetString() ?? string.Empty : string.Empty,
                json.TryGetProperty("status",   out var s) ? s.GetString() ?? string.Empty : string.Empty);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetBookingAsync failed for {Id}", bookingId);
            return null;
        }
    }
}
