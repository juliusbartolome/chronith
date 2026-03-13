---
title: Notifications
description: Configure notification channels and templates in Chronith.
---

Chronith supports three notification channels: Email (SMTP via MailKit), SMS (Twilio), and Push (Firebase Cloud Messaging).

## Email (SMTP)

Configure email notifications:

```sh
POST /v1/notifications/channels/email
Authorization: Bearer <token>
```

```json
{
  "smtpHost": "smtp.example.com",
  "smtpPort": 587,
  "smtpUsername": "notifications@mybusiness.com",
  "smtpPassword": "your-smtp-password",
  "fromAddress": "no-reply@mybusiness.com",
  "fromName": "My Business"
}
```

SMTP credentials are stored encrypted using AES-256-GCM.

## SMS (Twilio)

Configure SMS notifications:

```sh
POST /v1/notifications/channels/sms
Authorization: Bearer <token>
```

```json
{
  "accountSid": "ACxxxxxxxxxxxxxxxx",
  "authToken": "your-auth-token",
  "fromNumber": "+15550000000"
}
```

## Push (Firebase Cloud Messaging)

Configure push notifications:

```sh
POST /v1/notifications/channels/push
Authorization: Bearer <token>
```

```json
{
  "projectId": "my-firebase-project",
  "serviceAccountJson": "{ ... }"
}
```

## Notification templates

Templates use `{{variable}}` syntax for dynamic content.

```sh
POST /v1/notifications/templates
Authorization: Bearer <token>
```

```json
{
  "event": "booking.confirmed",
  "channel": "email",
  "subject": "Booking Confirmed: {{bookingType}}",
  "body": "Hi {{customerName}},\n\nYour booking for {{bookingType}} on {{startTime}} is confirmed.\n\nSee you then!"
}
```

### Available variables

| Variable | Description |
|----------|-------------|
| `{{customerName}}` | Customer's full name |
| `{{bookingType}}` | Name of the booking type |
| `{{startTime}}` | Booking start time |
| `{{endTime}}` | Booking end time |
| `{{staffName}}` | Assigned staff member name |
| `{{tenantName}}` | Business name |
