using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace InCleanHome.ReviewsService.Infrastructure.ExternalServices.IamService;

public interface IIamServiceClient
{
    Task<bool> SuspendUserAsync(int userId, int days, string reason, string adminBearerToken);
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
            using var req = new HttpRequestMessage(HttpMethod.Patch, $"{BaseUrl}/api/admin/users/{userId}/suspend");
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
}
