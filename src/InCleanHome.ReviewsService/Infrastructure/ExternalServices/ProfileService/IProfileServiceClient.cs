using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace InCleanHome.ReviewsService.Infrastructure.ExternalServices.ProfileService;

/// <summary>
///   HTTP client used by Reviews Service to synchronously update the worker's
///   running rating in Profile Service immediately after a review is saved.
/// </summary>
/// <remarks>
///   <para>
///     This is the fallback for the RabbitMQ event path: the broker is still
///     used to fan out notifications (Communication consumes it), but the
///     rating math is no longer trusted to the broker — we call Profile
///     Service directly. That matches the monolith behaviour where Reviews
///     called Profiles via an in-process ACL.
///   </para>
///   <para>
///     The endpoint lives at <c>POST /api/v1/internal/workers/{id}/register-review</c>
///     and is guarded by a shared <c>X-Internal-Token</c> header (set via env).
///   </para>
/// </remarks>
public interface IProfileServiceClient
{
    Task<bool> RegisterReviewAsync(int workerUserId, int rating);
}

public class ProfileServiceClient(
    HttpClient http,
    IConfiguration configuration,
    ILogger<ProfileServiceClient> logger) : IProfileServiceClient
{
    private string BaseUrl => configuration["Dependencies:ProfileServiceUrl"]
                              ?? "http://profile-service:5002";

    private string? InternalToken => Environment.GetEnvironmentVariable("INTERNAL_SERVICE_TOKEN")
                                     ?? configuration["Internal:ServiceToken"];

    public async Task<bool> RegisterReviewAsync(int workerUserId, int rating)
    {
        try
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Post,
                $"{BaseUrl}/api/v1/internal/workers/{workerUserId}/register-review");

            var token = InternalToken;
            if (!string.IsNullOrEmpty(token))
                req.Headers.Add("X-Internal-Token", token);

            var json = JsonSerializer.Serialize(new { rating });
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                logger.LogWarning(
                    "[ProfileServiceClient] RegisterReview returned {Status} for worker {WorkerId}: {Body}",
                    resp.StatusCode, workerUserId, body);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "[ProfileServiceClient] RegisterReview HTTP call failed for worker {WorkerId}",
                workerUserId);
            return false;
        }
    }
}
