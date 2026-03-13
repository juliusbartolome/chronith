---
title: Public Booking Page
description: Enable and embed the public booking page in Chronith.
---

The public booking page allows customers to book directly without needing a customer account.

## URL pattern

```
https://yourapp.com/book/{tenantSlug}
```

Or using the API:

```
GET /v1/public/{tenantSlug}
```

## Enable the public booking page

```sh
POST /v1/settings/public-booking
Authorization: Bearer <token>
```

```json
{
  "enabled": true,
  "allowGuestBookings": true
}
```

## Embedding the booking widget

Add the booking widget to any website with a single HTML snippet:

```html
<script
  src="https://yourapp.com/widget/booking.js"
  data-tenant="my-business"
  data-booking-type="consultation"
  data-theme="light"
></script>
<div id="chronith-booking-widget"></div>
```

### Widget attributes

| Attribute | Required | Description |
|-----------|----------|-------------|
| `data-tenant` | Yes | Your tenant slug |
| `data-booking-type` | No | Pre-select a booking type slug |
| `data-theme` | No | `light` (default) or `dark` |

## Branding

Customize the public booking page branding:

```sh
POST /v1/settings/branding
Authorization: Bearer <token>
```

```json
{
  "logoUrl": "https://yourapp.com/logo.png",
  "primaryColor": "#1a73e8",
  "businessName": "My Business"
}
```

The "Powered by Chronith" link appears on the public booking page for Free and Starter plans.
