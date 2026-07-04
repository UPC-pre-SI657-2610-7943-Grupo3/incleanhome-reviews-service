using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace InCleanHome.ReviewsService.Infrastructure.ExternalServices.IamService;

public interface IIamServiceClient
{
    Task<bool> SuspendUserAsync(int userId, int days, string reason, string adminBearerToken);

    /// <summary>
    /// Lifts a user's suspension. Called by Reviews when an admin accepts a
    /// SuspensionAppeal — the appeal acceptance must automatically clear the
    /// suspension on IAM (this is the monolith behaviour). The bearer must
    /// belong to an admin since /admin/users/{id}/clear-suspension is admin-only.
    /// </summary>
    Task<bool> ClearSuspensionAsync(int userId, string adminBearerToken);
}

public class IamServiceClient(
    HttpClient http,
    IConfiguration configuration,
    ILogger<IamServiceClient> logger) : IIamServiceClient
{
    private string BaseUrl => configuration["Dependencies:IamServiceUrl"] ?? "http://iam-service:5001";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> SuspendUserAsync(int userId, int days, string reason, string adminBearerToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Patch, $"{BaseUrl}/api/v1/admin/users/{userId}/suspend");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminBearerToken);
            req.Content = JsonContent.Create(new { days, reason }, options: JsonOptions);

            using var resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                logger.LogWarning("Suspend user {Id} -> {Status}", userId, resp.StatusCode);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SuspendUserAsync failed for {Id}", userId);
            return false;
        }
    }

    public async Task<bool> ClearSuspensionAsync(int userId, string adminBearerToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Patch,
                $"{BaseUrl}/api/v1/admin/users/{userId}/clear-suspension");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminBearerToken);

            using var resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                logger.LogWarning(
                    "PATCH /api/v1/admin/users/{Id}/clear-suspension -> {Status} {Body}",
                    userId, resp.StatusCode, body);
            }
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ClearSuspensionAsync failed for {Id}", userId);
            return false;
        }
    }
}
