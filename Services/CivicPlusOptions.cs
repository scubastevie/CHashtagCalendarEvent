namespace CivicPlusCalendar.Services;

public sealed class CivicPlusOptions
{
    public string ApiOrigin { get; set; } = "https://interview.civicplus.com";
    public string RequestPrefix { get; set; } = "";
    public string ClientSecret { get; set; } = "";
}
