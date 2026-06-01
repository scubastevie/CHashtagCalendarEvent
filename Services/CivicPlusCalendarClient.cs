using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CivicPlusCalendar.Models;
using Microsoft.Extensions.Options;

namespace CivicPlusCalendar.Services;

public interface ICivicPlusCalendarClient
{
    Task<EventList> ListEventsAsync(int skip, int top, CancellationToken cancellationToken);

    Task<CalendarEvent> CreateEventAsync(
        CreateEventRequest newEvent,
        CancellationToken cancellationToken);
}

public sealed class CivicPlusCalendarClient : ICivicPlusCalendarClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly CivicPlusOptions _options;
    private string? _accessToken;

    public CivicPlusCalendarClient(HttpClient httpClient, IOptions<CivicPlusOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<EventList> ListEventsAsync(int skip, int top, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        var url = BuildUrl("/api/Events", new Dictionary<string, string>
        {
            ["$orderBy"] = "startDate",
            ["$skip"] = skip.ToString(),
            ["$top"] = top.ToString()
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Request failed with status {(int)response.StatusCode}");
        }

        var data = await response.Content.ReadFromJsonAsync<EventList>(JsonOptions, cancellationToken);

        return data ?? new EventList();
    }

    public async Task<CalendarEvent> CreateEventAsync(
        CreateEventRequest newEvent,
        CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl("/api/Events"));

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(newEvent, options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Request failed with status {(int)response.StatusCode}");
        }

        var createdEvent = await response.Content.ReadFromJsonAsync<CalendarEvent>(
            JsonOptions,
            cancellationToken);

        return createdEvent ?? new CalendarEvent();
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken))
        {
            return _accessToken;
        }

        if (string.IsNullOrWhiteSpace(_options.RequestPrefix))
        {
            throw new InvalidOperationException("Missing CivicPlus:RequestPrefix configuration.");
        }

        if (string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new InvalidOperationException("Missing CivicPlus:ClientSecret configuration.");
        }

        var authRequest = new AuthRequest
        {
            ClientId = _options.RequestPrefix,
            ClientSecret = _options.ClientSecret
        };

        using var response = await _httpClient.PostAsJsonAsync(
            BuildUrl("/api/Auth"),
            authRequest,
            JsonOptions,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Request failed with status {(int)response.StatusCode}");
        }

        var data = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions, cancellationToken);
        _accessToken = data?.GetToken();

        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            throw new InvalidOperationException("Auth response did not include an access token.");
        }

        return _accessToken;
    }

    private string BuildUrl(string path, Dictionary<string, string>? query = null)
    {
        var cleanOrigin = _options.ApiOrigin.TrimEnd('/');
        var cleanPath = path.TrimStart('/');
        var url = $"{cleanOrigin}/{Uri.EscapeDataString(_options.RequestPrefix)}/{cleanPath}";

        if (query is null || query.Count == 0)
        {
            return url;
        }

        var queryString = string.Join(
            "&",
            query.Select(item =>
                $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));

        return $"{url}?{queryString}";
    }
}
