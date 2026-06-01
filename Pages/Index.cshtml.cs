using System.ComponentModel.DataAnnotations;
using System.Text;
using CivicPlusCalendar.Models;
using CivicPlusCalendar.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CivicPlusCalendar.Pages;

public sealed class IndexModel : PageModel
{
    private readonly CivicPlusCalendarClient _calendarClient;

    public IndexModel(CivicPlusCalendarClient calendarClient)
    {
        _calendarClient = calendarClient;
    }

    public List<CalendarEvent> Events { get; private set; } = [];
    public int TotalEvents { get; private set; }
    public string? ErrorMessage { get; private set; }

    [BindProperty(SupportsGet = true)]
    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 10;

    [BindProperty]
    public AddEventInput NewEvent { get; set; } = new();

    public int[] PageSizeOptions { get; } = [10, 25, 50];
    public int PageCount => Math.Max(1, (int)Math.Ceiling((double)TotalEvents / PageSize));
    public int FirstVisibleEvent => TotalEvents == 0 ? 0 : ((PageNumber - 1) * PageSize) + 1;
    public int LastVisibleEvent => Math.Min(PageNumber * PageSize, TotalEvents);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadEventsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        if (NewEvent.StartDate.HasValue && NewEvent.EndDate.HasValue &&
            NewEvent.EndDate <= NewEvent.StartDate)
        {
            ModelState.AddModelError("NewEvent.EndDate", "End date must be after the start date.");
        }

        if (!ModelState.IsValid)
        {
            await LoadEventsAsync(cancellationToken);
            return Page();
        }

        var eventRequest = new CreateEventRequest
        {
            Title = NewEvent.Title.Trim(),
            Description = NewEvent.Description.Trim(),
            StartDate = new DateTimeOffset(NewEvent.StartDate!.Value),
            EndDate = new DateTimeOffset(NewEvent.EndDate!.Value)
        };

        try
        {
            await _calendarClient.CreateEventAsync(eventRequest, cancellationToken);
        }
        catch (Exception error)
        {
            ModelState.AddModelError(string.Empty, error.Message);
            await LoadEventsAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage(new { pageNumber = 1, pageSize = PageSize });
    }

    public string GetGoogleCalendarUrl(CalendarEvent calendarEvent)
    {
        var url = new StringBuilder("https://calendar.google.com/calendar/render?action=TEMPLATE");

        url.Append("&text=").Append(Uri.EscapeDataString(calendarEvent.Title ?? "Untitled event"));
        url.Append("&dates=").Append(Uri.EscapeDataString(
            $"{FormatCalendarDate(calendarEvent.StartDate)}/{FormatCalendarDate(calendarEvent.EndDate)}"));

        if (!string.IsNullOrWhiteSpace(calendarEvent.Description))
        {
            url.Append("&details=").Append(Uri.EscapeDataString(calendarEvent.Description));
        }

        return url.ToString();
    }

    public string GetIcsDataUri(CalendarEvent calendarEvent)
    {
        var icsLines = new[]
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//CivicPlus Calendar Events Razor//EN",
            "CALSCALE:GREGORIAN",
            "METHOD:PUBLISH",
            "BEGIN:VEVENT",
            $"UID:{EscapeIcsText(calendarEvent.Id ?? $"{calendarEvent.Title}-{calendarEvent.StartDate}")}",
            $"DTSTAMP:{FormatCalendarDate(DateTimeOffset.UtcNow)}",
            $"DTSTART:{FormatCalendarDate(calendarEvent.StartDate)}",
            $"DTEND:{FormatCalendarDate(calendarEvent.EndDate)}",
            $"SUMMARY:{EscapeIcsText(calendarEvent.Title ?? "Untitled event")}",
            $"DESCRIPTION:{EscapeIcsText(calendarEvent.Description ?? "")}",
            "END:VEVENT",
            "END:VCALENDAR"
        };

        return $"data:text/calendar;charset=utf-8,{Uri.EscapeDataString(string.Join("\r\n", icsLines))}";
    }

    public string GetCalendarFileName(CalendarEvent calendarEvent)
    {
        var title = calendarEvent.Title ?? "calendar-event";
        var safeTitle = new string(title
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray()).Trim('-');

        return string.IsNullOrWhiteSpace(safeTitle) ? "calendar-event" : safeTitle;
    }

    public static bool CanExport(CalendarEvent calendarEvent)
    {
        return calendarEvent.StartDate.HasValue && calendarEvent.EndDate.HasValue;
    }

    private async Task LoadEventsAsync(CancellationToken cancellationToken)
    {
        PageSize = PageSizeOptions.Contains(PageSize) ? PageSize : 10;
        PageNumber = Math.Max(1, PageNumber);

        try
        {
            var eventList = await _calendarClient.ListEventsAsync(
                (PageNumber - 1) * PageSize,
                PageSize,
                cancellationToken);

            Events = eventList.Items.OrderBy(calendarEvent => calendarEvent.StartDate).ToList();
            TotalEvents = eventList.Total > 0 ? eventList.Total : Events.Count;
        }
        catch (Exception error)
        {
            ErrorMessage = error.Message;
            Events = [];
            TotalEvents = 0;
        }
    }

    private static string FormatCalendarDate(DateTimeOffset? dateValue)
    {
        return dateValue?.UtcDateTime.ToString("yyyyMMddTHHmmssZ") ?? "";
    }

    private static string EscapeIcsText(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n");
    }
}
