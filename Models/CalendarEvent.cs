using System.ComponentModel.DataAnnotations;

namespace CivicPlusCalendar.Models;

public sealed class CalendarEvent
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
}

public sealed class EventList
{
    public int Total { get; set; }
    public List<CalendarEvent> Items { get; set; } = [];
}

public sealed class CreateEventRequest
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
}

public sealed class AddEventInput
{
    [Required]
    [StringLength(100)]
    public string Title { get; set; } = "";

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = "";

    [Required]
    public DateTime? StartDate { get; set; }

    [Required]
    public DateTime? EndDate { get; set; }
}
