using System.Text;
using System.Text.Json;

namespace RaceDay.Mvc.Services;

// The ONLY class in the MVC project that talks to the Part 2 API.
// The MVC project never touches the database.
public class RaceDayApiClient
{
    private readonly IHttpClientFactory _factory;
    private readonly IHttpContextAccessor _accessor;

    // Web defaults = camelCase names and case-insensitive reading,
    // which matches what the API sends.
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public RaceDayApiClient(
        IHttpClientFactory factory,
        IHttpContextAccessor accessor)
    {
        _factory = factory;
        _accessor = accessor;
    }

    // ---------- Public helpers used by the controllers ----------

    public Task<ApiResult<T>> GetAsync<T>(string url)
        => SendAsync<T>(HttpMethod.Get, url, null);

    public Task<ApiResult<T>> PostAsync<T>(string url, object? body = null)
        => SendAsync<T>(HttpMethod.Post, url, ToJson(body));

    public async Task<ApiResult> PostAsync(string url, object? body = null)
        => await SendAsync<object>(HttpMethod.Post, url, ToJson(body));

    public async Task<ApiResult> PutAsync(string url, object? body = null)
        => await SendAsync<object>(HttpMethod.Put, url, ToJson(body));

    public Task<ApiResult<T>> PutAsync<T>(string url, object? body = null)
        => SendAsync<T>(HttpMethod.Put, url, ToJson(body));

    public async Task<ApiResult> DeleteAsync(string url)
        => await SendAsync<object>(HttpMethod.Delete, url, null);

    // ---------- Internals ----------

    private static StringContent? ToJson(object? body)
    {
        if (body == null) return null;

        string json = JsonSerializer.Serialize(body, body.GetType(), JsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method, string url, HttpContent? content)
    {
        try
        {
            var client = _factory.CreateClient("RaceDayApi");
            using var request = new HttpRequestMessage(method, url)
            {
                Content = content
            };

            // Send the API's session cookie so the API knows who we are.
            string? cookie = _accessor.HttpContext?
                .Session.GetString(SessionKeys.ApiCookie);

            if (!string.IsNullOrWhiteSpace(cookie))
            {
                request.Headers.TryAddWithoutValidation("Cookie", cookie);
            }

            using var response = await client.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();
            string? sessionCookie = ExtractSessionCookie(response);
            int status = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                return new ApiResult<T>
                {
                    Success = false,
                    StatusCode = status,
                    Error = BuildErrorMessage(status, body),
                    SessionCookie = sessionCookie
                };
            }

            // Calls that return no useful body (T = object) skip parsing.
            T? data = default;
            if (typeof(T) != typeof(object) && !string.IsNullOrWhiteSpace(body))
            {
                data = JsonSerializer.Deserialize<T>(body, JsonOptions);
            }

            return new ApiResult<T>
            {
                Success = true,
                StatusCode = status,
                Data = data,
                SessionCookie = sessionCookie
            };
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            // API is down, wrong address, timed out, or sent bad JSON.
            return new ApiResult<T>
            {
                Success = false,
                StatusCode = 0,
                Error = "RaceDay could not reach the server. " +
                        "Please try again in a moment."
            };
        }
    }

    // Turns an API error response into one friendly sentence.
    private static string BuildErrorMessage(int status, string body)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    // Your controllers return { "message": "..." }.
                    if (root.TryGetProperty("message", out var message) &&
                        message.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(message.GetString()))
                    {
                        return message.GetString()!;
                    }

                    // [ApiController] validation failures return
                    // { "errors": { "Field": [ "msg" ] } }.
                    if (root.TryGetProperty("errors", out var errors) &&
                        errors.ValueKind == JsonValueKind.Object)
                    {
                        var messages = errors.EnumerateObject()
                            .SelectMany(p => p.Value.EnumerateArray())
                            .Select(e => e.GetString())
                            .Where(s => !string.IsNullOrWhiteSpace(s));

                        string joined = string.Join(" ", messages);
                        if (joined.Length > 0) return joined;
                    }
                }
            }
        }
        catch (Exception ex) when (
            ex is JsonException || ex is InvalidOperationException)
        {
            // Body was not the JSON we expected: use the defaults below.
        }

        return status switch
        {
            400 => "Some of the details entered are not valid.",
            401 => "Please log in to continue.",
            403 => "You do not have permission to do that.",
            404 => "We could not find what you were looking for.",
            409 => "That conflicts with existing information.",
            _ => "Something went wrong. Please try again."
        };
    }

    // The API sets ".AspNetCore.Session=...; path=/; httponly".
    // A request's Cookie header only needs "name=value".
    private static string? ExtractSessionCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
            return null;

        string? header = values.FirstOrDefault(v => v.StartsWith(
            ".AspNetCore.Session=", StringComparison.OrdinalIgnoreCase));

        return header?.Split(';', 2)[0];
    }
}