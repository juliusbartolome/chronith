---
title: C# SDK
description: Official C#/.NET SDK for the Chronith API.
---

## Installation

```sh
dotnet add package Chronith.Client
```

## Dependency injection (recommended)

Register the client in your `Program.cs`:

```csharp
builder.Services.AddChronithClient(options =>
{
    options.BaseUrl = "https://api.yourdomain.com";
    options.ApiKey = "chron_live_xxxxxxxxxxxxxxxx";
});
```

Then inject `ChronithClient` into your services:

```csharp
public class BookingService(ChronithClient chronith)
{
    public async Task<BookingDto> CreateBookingAsync(CreateBookingRequest request)
    {
        return await chronith.Bookings.CreateAsync(request);
    }
}
```

## Without dependency injection

```csharp
var client = new ChronithClient(new ChronithClientOptions
{
    BaseUrl = "https://api.yourdomain.com",
    ApiKey = "chron_live_xxxxxxxxxxxxxxxx",
});

var bookingTypes = await client.BookingTypes.ListAsync();
```

## Error handling

```csharp
using Chronith.Client.Exceptions;

try
{
    var booking = await client.Bookings.GetAsync("nonexistent-id");
}
catch (ChronithApiException ex)
{
    Console.WriteLine($"Status: {ex.StatusCode}");
    Console.WriteLine($"Response: {ex.ResponseBody}");
}
```

`ChronithApiException` exposes:
- `StatusCode` — HTTP status code (int)
- `ResponseBody` — raw JSON response body (string)
- `ProblemDetails` — deserialized RFC 7807 details (optional)

## Available services

| Property | Description |
|----------|-------------|
| `client.Bookings` | Create, get, confirm, cancel, reschedule bookings |
| `client.BookingTypes` | Manage booking types |
| `client.Availability` | Query available slots |
| `client.Staff` | Manage staff members |
| `client.Analytics` | Query analytics data |
| `client.Webhooks` | Manage webhooks |
| `client.Tenant` | Tenant settings and subscriptions |
| `client.Recurring` | Create and manage recurring booking series |
| `client.Audit` | Query audit logs |
| `client.Notifications` | Configure notification channels and templates |
