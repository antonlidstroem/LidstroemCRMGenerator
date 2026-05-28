using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Lidstroem.Frontend.Core.Auth;

namespace Lidstroem.Frontend.Core.Services;

/// <summary>
/// Generic API client used by all entity CRUD operations.
/// Automatically injects Authorization header and handles token refresh on 401.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private readonly AuthService _auth;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public ApiClient(HttpClient http, AuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    // BUG-37 FIX: Exposes total item count from X-Total-Count response header.
    // Backend sets this header on paginated endpoints. Used by EntityList to
    // calculate TotalPages without fetching all records.
    public int LastTotalCount { get; private set; }

    public async Task<List<JsonElement>?> GetListAsync(string url)
    {
        var response = await SendAsync(HttpMethod.Get, url);
        if (!response.IsSuccessStatusCode) return null;

        LastTotalCount = response.Headers.TryGetValues("X-Total-Count", out var vals)
            && int.TryParse(vals.FirstOrDefault(), out var count) ? count : 0;

        return await response.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions);
    }

    public async Task<JsonElement?> GetOneAsync(string url)
    {
        var response = await SendAsync(HttpMethod.Get, url);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
    }

    public async Task<JsonElement?> PostAsync(string url, object body)
    {
        var response = await SendAsync(HttpMethod.Post, url, body);
        if (!response.IsSuccessStatusCode) return null;

        // BUG-16 FIX: ReadFromJsonAsync<JsonElement> throws JsonException on 204 No Content
        // because there are no JSON tokens in an empty body. Endpoints like /api/rbac/revoke
        // return 204 — callers that check (result == null) interpreted the exception as a
        // failure and showed an error message even though the operation succeeded.
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent
         || response.Content.Headers.ContentLength == 0)
            return JsonDocument.Parse("{}").RootElement;

        try
        {
            return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ApiClient] JSON parse error POST {url}: {ex.Message}");
            // Return empty object so callers get non-null (= success) without crashing.
            return JsonDocument.Parse("{}").RootElement;
        }
    }

    public async Task<bool> PutAsync(string url, object body)
    {
        var response = await SendAsync(HttpMethod.Put, url, body);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(string url)
    {
        var response = await SendAsync(HttpMethod.Delete, url);
        return response.IsSuccessStatusCode;
    }

    // BUG-29 FIX: Authenticated binary download — carries Bearer token unlike <a href>.
    public async Task<byte[]?> GetBytesAsync(string url)
    {
        try
        {
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ApiClient] GetBytesAsync {url}: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> PatchAsync(string url, object body)
    {
        var response = await SendAsync(HttpMethod.Patch, url, body);
        return response.IsSuccessStatusCode;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, object? body = null)
    {
        HttpResponseMessage response;
        HttpRequestMessage request;
        try
        {
            request = BuildRequest(method, url, body);
            response = await _http.SendAsync(request);
        }
        catch (Exception ex)
        {
            // Network error (ERR_CONNECTION_REFUSED, timeout, etc.)
            // Return a synthetic 503 so callers get null/false without crashing.
            Console.Error.WriteLine($"[ApiClient] Network error {method} {url}: {ex.Message}");
            return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            var refreshed = await _auth.TryRefreshAsync();
            if (refreshed)
            {
                try
                {
                    request = BuildRequest(method, url, body);
                    response = await _http.SendAsync(request);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[ApiClient] Retry network error {method} {url}: {ex.Message}");
                    return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
                }
            }
        }

        return response;
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url, object? body)
    {
        var request = new HttpRequestMessage(method, url);
        var authHeader = _auth.GetAuthorizationHeader();
        if (authHeader != null)
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);

        if (body != null)
            request.Content = JsonContent.Create(body, options: JsonOptions);

        return request;
    }
}
