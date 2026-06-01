using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CivicPlusCalendar.Models;
using CivicPlusCalendar.Services;
using Microsoft.Extensions.Options;

namespace CivicPlusCalendar.Tests;

public sealed class CivicPlusCalendarClientTests
{
    [Fact]
    public async Task ListEventsAsync_AuthenticatesAndRequestsRequestedPage()
    {
        var handler = new RecordingHandler();
        handler.EnqueueJson(new { access_token = "token-123" });
        handler.EnqueueJson(new
        {
            total = 2,
            items = new[]
            {
                new { id = "event-1", title = "Town Hall", startDate = "2026-02-01T15:00:00Z" }
            }
        });

        var client = CreateClient(handler);

        var result = await client.ListEventsAsync(skip: 20, top: 10, CancellationToken.None);

        Assert.Equal(2, result.Total);
        Assert.Single(result.Items);
        Assert.Equal("Town Hall", result.Items[0].Title);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("https://example.test/root/civic%20calendar/api/Auth", handler.Requests[0].Url);
        Assert.Contains("\"clientId\":\"civic calendar\"", handler.Requests[0].Body);
        Assert.Contains("\"clientSecret\":\"top-secret\"", handler.Requests[0].Body);

        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
        Assert.Equal("Bearer", handler.Requests[1].AuthorizationScheme);
        Assert.Equal("token-123", handler.Requests[1].AuthorizationParameter);
        Assert.StartsWith("https://example.test/root/civic%20calendar/api/Events?", handler.Requests[1].Url);
        Assert.Contains("%24orderBy=startDate", handler.Requests[1].Url);
        Assert.Contains("%24skip=20", handler.Requests[1].Url);
        Assert.Contains("%24top=10", handler.Requests[1].Url);
    }

    [Fact]
    public async Task ListEventsAsync_ReusesAccessTokenForLaterRequests()
    {
        var handler = new RecordingHandler();
        handler.EnqueueJson(new { token = "cached-token" });
        handler.EnqueueJson(new { total = 0, items = Array.Empty<object>() });
        handler.EnqueueJson(new { total = 0, items = Array.Empty<object>() });

        var client = CreateClient(handler);

        await client.ListEventsAsync(0, 10, CancellationToken.None);
        await client.ListEventsAsync(10, 10, CancellationToken.None);

        Assert.Equal(3, handler.Requests.Count);
        Assert.Single(handler.Requests, request => request.Url.EndsWith("/api/Auth", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateEventAsync_PostsEventAndReturnsCreatedEvent()
    {
        var handler = new RecordingHandler();
        handler.EnqueueJson(new { accessToken = "token-123" });
        handler.EnqueueJson(new
        {
            id = "created-1",
            title = "Created Event",
            description = "Saved",
            startDate = "2026-03-01T14:00:00Z",
            endDate = "2026-03-01T15:00:00Z"
        }, HttpStatusCode.Created);

        var client = CreateClient(handler);
        var request = new CreateEventRequest
        {
            Title = "Created Event",
            Description = "Saved",
            StartDate = new DateTimeOffset(2026, 3, 1, 14, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 3, 1, 15, 0, 0, TimeSpan.Zero)
        };

        var result = await client.CreateEventAsync(request, CancellationToken.None);

        Assert.Equal("created-1", result.Id);
        Assert.Equal("Created Event", result.Title);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.Equal("https://example.test/root/civic%20calendar/api/Events", handler.Requests[1].Url);
        Assert.Equal("Bearer", handler.Requests[1].AuthorizationScheme);
        Assert.Equal("token-123", handler.Requests[1].AuthorizationParameter);
        Assert.Contains("\"title\":\"Created Event\"", handler.Requests[1].Body);
        Assert.Contains("\"description\":\"Saved\"", handler.Requests[1].Body);
    }

    [Fact]
    public async Task ListEventsAsync_ThrowsWhenCredentialsAreMissing()
    {
        var client = new CivicPlusCalendarClient(
            new HttpClient(new RecordingHandler()),
            Options.Create(new CivicPlusOptions { ApiOrigin = "https://example.test", RequestPrefix = "prefix" }));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.ListEventsAsync(0, 10, CancellationToken.None));

        Assert.Equal("Missing CivicPlus:ClientSecret configuration.", error.Message);
    }

    private static CivicPlusCalendarClient CreateClient(RecordingHandler handler)
    {
        return new CivicPlusCalendarClient(
            new HttpClient(handler),
            Options.Create(new CivicPlusOptions
            {
                ApiOrigin = "https://example.test/root/",
                RequestPrefix = "civic calendar",
                ClientSecret = "top-secret"
            }));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly Queue<HttpResponseMessage> _responses = new();

        public List<RecordedRequest> Requests { get; } = [];

        public void EnqueueJson(object response, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responses.Enqueue(new HttpResponseMessage(statusCode)
            {
                Content = JsonContent.Create(response, options: JsonOptions)
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri?.AbsoluteUri ?? "",
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                body));

            if (_responses.Count == 0)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return _responses.Dequeue();
        }
    }

    private sealed record RecordedRequest(
        HttpMethod Method,
        string Url,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string Body);
}
