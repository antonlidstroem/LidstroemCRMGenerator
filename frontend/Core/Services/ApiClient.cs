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

    public async Task<List<JsonElement>?> GetListAsync(string url)
    {
        var response = await SendAsync(HttpMethod.Get, url);
        if (!response.IsSuccessStatusCode) return null;
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
        return await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
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

    public async Task<bool> PatchAsync(string url, object body)
    {
        var response = await SendAsync(HttpMethod.Patch, url, body);
        return response.IsSuccessStatusCode;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, object? body = null)
    {
        var request = BuildRequest(method, url, body);
        var response = await _http.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            var refreshed = await _auth.TryRefreshAsync();
            if (refreshed)
            {
                // Safe to rebuild the request: JsonContent.Create() serialises the body
                // object fresh each time — it does not consume a one-time stream.
                // The original HttpRequestMessage cannot be reused after SendAsync.
                request = BuildRequest(method, url, body);
                response = await _http.SendAsync(request);
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
