# Calendar Events Razor

This is a small C# Razor Pages version of the calendar app.

The important difference from the Vite version is that CivicPlus auth happens on the server. The browser calls this Razor app, and the Razor app calls CivicPlus.

```text
Browser
  -> Razor Pages app
  -> CivicPlus API
```

## Configuration

Do not put real secrets in `appsettings.json`.

For local development, use environment variables:

```bash
export CivicPlus__ApiOrigin="https://interview.civicplus.com"
export CivicPlus__RequestPrefix="your_request_prefix"
export CivicPlus__ClientSecret="your_client_secret"
```

Or use .NET user secrets:

```bash
dotnet user-secrets init
dotnet user-secrets set "CivicPlus:RequestPrefix" "your_request_prefix"
dotnet user-secrets set "CivicPlus:ClientSecret" "your_client_secret"
```

## Run

```bash
dotnet restore
dotnet run
```

Then open the local URL printed by `dotnet run`.

## Files

- `Pages/Index.cshtml` renders the event list and add-event form.
- `Pages/Index.cshtml.cs` handles page load, pagination, and form submit.
- `Services/CivicPlusCalendarClient.cs` contains reusable API calls.
- `Models/` contains simple request and response models.
