---
title: Notifications API
description: REST endpoints for managing notification channels and templates in Chronith.
---

## Endpoints

| Method | Path | Description | Auth |
|--------|------|-------------|------|
| `GET` | `/v1/notifications/channels` | List configured channels | Admin |
| `POST` | `/v1/notifications/channels/email` | Configure email channel | Admin |
| `PUT` | `/v1/notifications/channels/email` | Update email channel | Admin |
| `DELETE` | `/v1/notifications/channels/email` | Remove email channel | Admin |
| `POST` | `/v1/notifications/channels/sms` | Configure SMS channel | Admin |
| `PUT` | `/v1/notifications/channels/sms` | Update SMS channel | Admin |
| `DELETE` | `/v1/notifications/channels/sms` | Remove SMS channel | Admin |
| `POST` | `/v1/notifications/channels/push` | Configure push channel | Admin |
| `PUT` | `/v1/notifications/channels/push` | Update push channel | Admin |
| `DELETE` | `/v1/notifications/channels/push` | Remove push channel | Admin |
| `GET` | `/v1/notifications/templates` | List templates | Admin |
| `POST` | `/v1/notifications/templates` | Create template | Admin |
| `PUT` | `/v1/notifications/templates/{id}` | Update template | Admin |
| `DELETE` | `/v1/notifications/templates/{id}` | Delete template | Admin |

## Request body: Email channel

```json
{
  "smtpHost": "string",
  "smtpPort": "integer",
  "smtpUsername": "string",
  "smtpPassword": "string",
  "fromAddress": "string",
  "fromName": "string"
}
```

## Request body: Template

```json
{
  "event": "booking.confirmed",
  "channel": "email | sms | push",
  "subject": "string (email only)",
  "body": "string (supports {{variable}} interpolation)"
}
```
