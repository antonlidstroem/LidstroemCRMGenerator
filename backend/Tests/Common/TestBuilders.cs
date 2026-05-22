using System.Net.Http.Json;
using System.Text.Json;

namespace Lidstroem.Tests.Common;

// ── Generic response wrapper ──────────────────────────────────────────────────

public record CreatedResponse(int Id);

// ── ActorBuilder ─────────────────────────────────────────────────────────────

public class ActorBuilder
{
    private string _displayName = "Test Actor";
    private string _email       = $"actor-{Guid.NewGuid():N}@test.local";
    private string? _phone;

    public ActorBuilder WithName(string name)  { _displayName = name; return this; }
    public ActorBuilder WithEmail(string email) { _email = email; return this; }
    public ActorBuilder WithPhone(string phone) { _phone = phone; return this; }

    public async Task<int> BuildAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/actors", new
        {
            DisplayName = _displayName,
            Email       = _email,
            PhoneNumber = _phone
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CreatedResponse>();
        return body!.Id;
    }
}

// ── ProjectBuilder ────────────────────────────────────────────────────────────

public class ProjectBuilder
{
    private string _title       = "Test Project";
    private string _description = "Test description";

    public ProjectBuilder WithTitle(string title)             { _title = title; return this; }
    public ProjectBuilder WithDescription(string description) { _description = description; return this; }

    public async Task<int> BuildAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/projects", new
        {
            Title       = _title,
            Description = _description
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CreatedResponse>();
        return body!.Id;
    }
}

// ── ActivityBuilder ───────────────────────────────────────────────────────────

public class ActivityBuilder
{
    private string   _title     = "Test Activity";
    private int      _projectId;
    private DateTime? _start;
    private DateTime? _end;

    public ActivityBuilder WithTitle(string title)      { _title = title; return this; }
    public ActivityBuilder ForProject(int projectId)    { _projectId = projectId; return this; }
    public ActivityBuilder StartingAt(DateTime start)   { _start = start; return this; }
    public ActivityBuilder EndingAt(DateTime end)       { _end = end; return this; }

    public async Task<int> BuildAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/activities", new
        {
            Title     = _title,
            ProjectId = _projectId,
            StartDate = _start,
            EndDate   = _end
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CreatedResponse>();
        return body!.Id;
    }
}

// ── DonationBuilder ───────────────────────────────────────────────────────────

public class DonationBuilder
{
    private decimal _amount     = 100m;
    private string  _currency   = "SEK";
    private int?    _donorId;
    private string? _donorType;
    private int?    _targetId;
    private string? _targetType;

    public DonationBuilder WithAmount(decimal amount)        { _amount = amount; return this; }
    public DonationBuilder WithCurrency(string currency)     { _currency = currency; return this; }
    public DonationBuilder FromActor(int actorId)            { _donorId = actorId; _donorType = "Actor"; return this; }
    public DonationBuilder ToProject(int projectId)          { _targetId = projectId; _targetType = "Project"; return this; }
    public DonationBuilder ToActivity(int activityId)        { _targetId = activityId; _targetType = "Activity"; return this; }

    public async Task<int> BuildAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/donations", new
        {
            Amount     = _amount,
            Currency   = _currency,
            DonorId    = _donorId,
            DonorType  = _donorType,
            TargetId   = _targetId,
            TargetType = _targetType
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CreatedResponse>();
        return body!.Id;
    }
}

// ── HttpClient extension helpers ──────────────────────────────────────────────

public static class HttpClientTestExtensions
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<JsonElement> GetJsonAsync(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content).RootElement;
    }

    public static async Task<int> PostAndGetIdAsync(this HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreatedResponse>(JsonOpts);
        return result!.Id;
    }

    /// <summary>
    /// Asserts that the response has exactly the expected status code.
    /// Returns the response for further assertions.
    /// </summary>
    public static async Task<HttpResponseMessage> ShouldReturn(
        this Task<HttpResponseMessage> responseTask, System.Net.HttpStatusCode expected)
    {
        var response = await responseTask;
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception(
                $"Expected {(int)expected} {expected} but got " +
                $"{(int)response.StatusCode} {response.StatusCode}.\nBody: {body}");
        }
        return response;
    }
}
