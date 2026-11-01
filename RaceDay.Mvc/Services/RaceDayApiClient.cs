using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace RaceDay.Mvc.Services;

// The ONLY class in the MVC project that talks to the Part 2 API.
// The MVC project never connects directly to the database or Azure.
public class RaceDayApiClient
{
    private readonly IHttpClientFactory _factory;
    private readonly IHttpContextAccessor _accessor;

private static readonly JsonSerializerOptions JsonOptions =
    new(JsonSerializerDefaults.Web);

    public RaceDayApiClient(
        IHttpClientFactory factory,
        IHttpContextAccessor accessor)
    {
        _factory = factory;
        _accessor = accessor;
    }

    // ---------- GET ----------

    public Task<ApiResult<T>> GetAsync<T>(string url)
        => SendAsync<T>(HttpMethod.Get, url, null);

    // ---------- POST JSON ----------

    public Task<ApiResult<T>> PostAsync<T>(
        string url,
        object? body = null)
        => SendAsync<T>(HttpMethod.Post, url, ToJson(body));

    public async Task<ApiResult> PostAsync(
        string url,
        object? body = null)
        => await SendAsync<object>(
            HttpMethod.Post,
            url,
            ToJson(body));

    // ---------- POST FILE ----------

    // Sends an uploaded file to the API using multipart/form-data.
    // The form field name is "file", matching the API upload endpoints.
    public async Task<ApiResult<T>> PostFileAsync<T>(
        string url,
        IFormFile file)
    {
        using var multipart = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();

        var fileContent = new StreamContent(stream);

        if (!string.IsNullOrWhiteSpace(file.ContentType))
        {
            fileContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(
                    file.ContentType);
        }

        multipart.Add(
            fileContent,
            "file",
            Path.GetFileName(file.FileName));

        return await SendAsync<T>(
            HttpMethod.Post,
            url,
            multipart);
    }

    // ---------- PUT ----------

    public async Task<ApiResult> PutAsync(
        string url,
        object? body = null)
        => await SendAsync<object>(
            HttpMethod.Put,
            url,
            ToJson(body));

    public Task<ApiResult<T>> PutAsync<T>(
        string url,
        object? body = null)
        => SendAsync<T>(
            HttpMethod.Put,
            url,
            ToJson(body));

    // ---------- DELETE ----------

    public async Task<ApiResult> DeleteAsync(string url)
        => await SendAsync<object>(
            HttpMethod.Delete,
            url,
            null);

    // ---------- JSON CONVERSION ----------

    private static StringContent? ToJson(object? body)
    {
        if (body == null)
            return null;

        string json = JsonSerializer.Serialize(
            body,
            body.GetType(),
            JsonOptions);

        return new StringContent(
            json,
            Encoding.UTF8,
            "application/json");
    }

    // ---------- SEND REQUEST ----------

    private async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method,
        string url,
        HttpContent? content)
    {
        try
        {
            var client = _factory.CreateClient("RaceDayApi");

            using var request = new HttpRequestMessage(method, url)
            {
                Content = content
            };

            // Forward the API session cookie.
            string? cookie = _accessor.HttpContext?
                .Session.GetString(SessionKeys.ApiCookie);

            if (!string.IsNullOrWhiteSpace(cookie))
            {
                request.Headers.TryAddWithoutValidation(
                    "Cookie",
                    cookie);
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

            T? data = default;

            if (typeof(T) != typeof(object) &&
                !string.IsNullOrWhiteSpace(body))
            {
                data = JsonSerializer.Deserialize<T>(
                    body,
                    JsonOptions);
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
            return new ApiResult<T>
            {
                Success = false,
                StatusCode = 0,
                Error =
                    "RaceDay could not reach the server. Please try again in a moment."
            };
        }
    }

    // ---------- ERROR MESSAGES ----------

    private static string BuildErrorMessage(
        int status,
        string body)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var doc = JsonDocument.Parse(body);

                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty(
                            "message",
                            out var message) &&
                        message.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(message.GetString()))
                    {
                        return message.GetString()!;
                    }

                    if (root.TryGetProperty(
                            "errors",
                            out var errors) &&
                        errors.ValueKind == JsonValueKind.Object)
                    {
                        var messages = errors
                            .EnumerateObject()
                            .SelectMany(p => p.Value.EnumerateArray())
                            .Select(e => e.GetString())
                            .Where(s => !string.IsNullOrWhiteSpace(s));

                        string joined = string.Join(" ", messages);

                        if (joined.Length > 0)
                            return joined;
                    }
                }
            }
        }
        catch (Exception ex) when (
            ex is JsonException ||
            ex is InvalidOperationException)
        {
            // Use the default message if the response is not valid JSON.
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

    // ---------- SESSION COOKIE ----------

    private static string? ExtractSessionCookie(
        HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(
                "Set-Cookie",
                out var values))
        {
            return null;
        }

        string? header = values.FirstOrDefault(v =>
            v.StartsWith(
                ".AspNetCore.Session=",
                StringComparison.OrdinalIgnoreCase));

        return header?.Split(';', 2)[0];
    }


}
