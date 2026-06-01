using CivicPlusCalendar.Models;
using CivicPlusCalendar.Pages;
using CivicPlusCalendar.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CivicPlusCalendar.Tests;

public sealed class IndexModelTests
{
    [Fact]
    public async Task OnGetAsync_LoadsEventsSortedByStartDateAndNormalizesPaging()
    {
        var calendarClient = new FakeCalendarClient
        {
            EventsToReturn = new EventList
            {
                Items =
                [
                    new CalendarEvent
                    {
                        Id = "later",
                        Title = "Later",
                        StartDate = new DateTimeOffset(2026, 5, 2, 12, 0, 0, TimeSpan.Zero)
                    },
                    new CalendarEvent
                    {
                        Id = "earlier",
                        Title = "Earlier",
                        StartDate = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero)
                    }
                ]
            }
        };
        var model = new IndexModel(calendarClient)
        {
            PageNumber = -5,
            PageSize = 999
        };

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal(1, model.PageNumber);
        Assert.Equal(10, model.PageSize);
        Assert.Equal(0, calendarClient.LastSkip);
        Assert.Equal(10, calendarClient.LastTop);
        Assert.Equal(2, model.TotalEvents);
        Assert.Equal(
            new[] { "Earlier", "Later" },
            model.Events.Select(calendarEvent => calendarEvent.Title ?? "").ToArray());
    }

    [Fact]
    public async Task OnGetAsync_StoresErrorMessageWhenClientFails()
    {
        var model = new IndexModel(new FakeCalendarClient
        {
            ListError = new InvalidOperationException("API unavailable")
        });

        await model.OnGetAsync(CancellationToken.None);

        Assert.Equal("API unavailable", model.ErrorMessage);
        Assert.Empty(model.Events);
        Assert.Equal(0, model.TotalEvents);
    }

    [Fact]
    public async Task OnPostCreateAsync_AddsValidationErrorWhenEndDateIsNotAfterStartDate()
    {
        var calendarClient = new FakeCalendarClient();
        var model = new IndexModel(calendarClient)
        {
            NewEvent = new AddEventInput
            {
                Title = "Council Meeting",
                Description = "Monthly meeting",
                StartDate = new DateTime(2026, 4, 1, 10, 0, 0),
                EndDate = new DateTime(2026, 4, 1, 10, 0, 0)
            }
        };

        var result = await model.OnPostCreateAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(model.ModelState.IsValid);
        Assert.True(model.ModelState.ContainsKey("NewEvent.EndDate"));
        Assert.Equal(0, calendarClient.CreateCallCount);
    }

    [Fact]
    public async Task OnPostCreateAsync_CreatesTrimmedEventAndRedirectsToFirstPage()
    {
        var calendarClient = new FakeCalendarClient();
        var model = new IndexModel(calendarClient)
        {
            PageSize = 25,
            NewEvent = new AddEventInput
            {
                Title = "  Library Board  ",
                Description = "  Agenda review  ",
                StartDate = new DateTime(2026, 4, 1, 10, 0, 0),
                EndDate = new DateTime(2026, 4, 1, 11, 0, 0)
            }
        };

        var expectedStart = new DateTimeOffset(model.NewEvent.StartDate!.Value);
        var expectedEnd = new DateTimeOffset(model.NewEvent.EndDate!.Value);

        var result = await model.OnPostCreateAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(1, redirect.RouteValues?["pageNumber"]);
        Assert.Equal(25, redirect.RouteValues?["pageSize"]);
        Assert.Equal(1, calendarClient.CreateCallCount);
        Assert.Equal("Library Board", calendarClient.CreatedEvent?.Title);
        Assert.Equal("Agenda review", calendarClient.CreatedEvent?.Description);
        Assert.Equal(expectedStart, calendarClient.CreatedEvent?.StartDate);
        Assert.Equal(expectedEnd, calendarClient.CreatedEvent?.EndDate);
    }

    [Fact]
    public void CalendarExportHelpers_FormatSafeUrlsAndFileNames()
    {
        var model = new IndexModel(new FakeCalendarClient());
        var calendarEvent = new CalendarEvent
        {
            Id = "event-123",
            Title = "Planning, Launch; Phase",
            Description = "Line one\nLine two",
            StartDate = new DateTimeOffset(2026, 6, 1, 14, 30, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 6, 1, 15, 30, 0, TimeSpan.Zero)
        };

        var googleUrl = model.GetGoogleCalendarUrl(calendarEvent);
        var icsData = model.GetIcsDataUri(calendarEvent);
        var decodedIcs = Uri.UnescapeDataString(icsData["data:text/calendar;charset=utf-8,".Length..]);

        Assert.Equal("planning--launch--phase", model.GetCalendarFileName(calendarEvent));
        Assert.Contains("text=Planning%2C%20Launch%3B%20Phase", googleUrl);
        Assert.Contains("dates=20260601T143000Z%2F20260601T153000Z", googleUrl);
        Assert.Contains("SUMMARY:Planning\\, Launch\\; Phase", decodedIcs);
        Assert.Contains("DESCRIPTION:Line one\\nLine two", decodedIcs);
    }

    [Fact]
    public void CanExport_RequiresStartAndEndDates()
    {
        Assert.False(IndexModel.CanExport(new CalendarEvent()));
        Assert.False(IndexModel.CanExport(new CalendarEvent { StartDate = DateTimeOffset.UtcNow }));
        Assert.True(IndexModel.CanExport(new CalendarEvent
        {
            StartDate = DateTimeOffset.UtcNow,
            EndDate = DateTimeOffset.UtcNow.AddHours(1)
        }));
    }

    private sealed class FakeCalendarClient : ICivicPlusCalendarClient
    {
        public EventList EventsToReturn { get; set; } = new();
        public Exception? ListError { get; set; }
        public CreateEventRequest? CreatedEvent { get; private set; }
        public int CreateCallCount { get; private set; }
        public int LastSkip { get; private set; }
        public int LastTop { get; private set; }

        public Task<EventList> ListEventsAsync(int skip, int top, CancellationToken cancellationToken)
        {
            if (ListError is not null)
            {
                throw ListError;
            }

            LastSkip = skip;
            LastTop = top;

            return Task.FromResult(EventsToReturn);
        }

        public Task<CalendarEvent> CreateEventAsync(
            CreateEventRequest newEvent,
            CancellationToken cancellationToken)
        {
            CreateCallCount++;
            CreatedEvent = newEvent;

            return Task.FromResult(new CalendarEvent
            {
                Id = "created",
                Title = newEvent.Title,
                Description = newEvent.Description,
                StartDate = newEvent.StartDate,
                EndDate = newEvent.EndDate
            });
        }
    }
}
