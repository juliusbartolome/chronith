# Chronith.Client — C# SDK

Official C# client SDK for the [Chronith](https://github.com/juliusbartolome/chronith) booking engine API.

## Installation

```bash
dotnet add package Chronith.Client
```

## Quick Start

### With Dependency Injection (recommended)

```csharp
// Program.cs
using Chronith.Client.Extensions;

builder.Services.AddChronithClient(options =>
{
    options.BaseUrl = "https://api.example.com";
    options.ApiKey  = "your-api-key";
    // or use JWT:
    // options.JwtToken = "your-bearer-token";
});
```

Inject `ChronithClient` anywhere:

```csharp
public class BookingController(ChronithClient chronith)
{
    public async Task<IActionResult> ListBookings(CancellationToken ct)
    {
        var result = await chronith.Bookings.ListAsync(page: 1, pageSize: 20, ct);
        return Ok(result);
    }
}
```

### Without Dependency Injection

```csharp
using var httpClient = new HttpClient
{
    BaseAddress = new Uri("https://api.example.com/"),
    Timeout     = TimeSpan.FromSeconds(30),
};
httpClient.DefaultRequestHeaders.Add("X-Api-Key", "your-api-key");

var chronith = new ChronithClient(httpClient);

var bookings = await chronith.Bookings.ListAsync();
```

## Available Services

| Property | Service | Description |
|---|---|---|
| `client.Bookings` | `BookingsService` | List, get, create, cancel, confirm, reschedule bookings |
| `client.BookingTypes` | `BookingTypesService` | Manage booking type definitions |
| `client.Staff` | `StaffService` | Manage staff members |
| `client.Availability` | `AvailabilityService` | Query available time slots |
| `client.Analytics` | `AnalyticsService` | Booking analytics and reports |
| `client.Webhooks` | `WebhooksService` | Manage webhook subscriptions |
| `client.Notifications` | `NotificationsService` | Configure notification channels |
| `client.Recurring` | `RecurringService` | Manage recurring booking series |
| `client.Audit` | `AuditService` | Access audit log entries |
| `client.Tenant` | `TenantService` | Tenant settings, plans, subscription, usage |

## Error Handling

All API errors throw `ChronithApiException`:

```csharp
using Chronith.Client.Errors;

try
{
    var booking = await chronith.Bookings.GetAsync(bookingId, ct);
}
catch (ChronithApiException ex)
{
    Console.WriteLine($"HTTP {(int)ex.StatusCode}: {ex.ResponseBody}");
    // ex.StatusCode is System.Net.HttpStatusCode
    // ex.ResponseBody is the raw JSON error body
}
```

## Configuration Options

| Option | Type | Default | Description |
|---|---|---|---|
| `BaseUrl` | `string` | *(required)* | Base URL of the Chronith API |
| `ApiKey` | `string?` | `null` | API key sent as `X-Api-Key` header |
| `JwtToken` | `string?` | `null` | Bearer JWT token |
| `Timeout` | `TimeSpan` | 30 seconds | HTTP request timeout |
| `MaxRetries` | `int` | 3 | Max retries on transient failures |

`ApiKey` and `JwtToken` are mutually exclusive. If both are set, `ApiKey` takes precedence.

## Requirements

- .NET 10.0+
- `Microsoft.Extensions.Http` 10.0.0

## License

MIT
