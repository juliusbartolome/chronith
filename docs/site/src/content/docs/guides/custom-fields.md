---
title: Custom Fields
description: Define and validate custom fields on booking types in Chronith.
---

Custom fields allow you to collect additional information from customers at booking time.

## Define custom fields on a booking type

```sh
POST /v1/booking-types/{id}/custom-fields
Authorization: Bearer <token>
```

```json
{
  "name": "pet_name",
  "label": "Pet Name",
  "type": "Text",
  "required": true
}
```

## Field types

| Type | Description |
|------|-------------|
| `Text` | Single-line text input |
| `TextArea` | Multi-line text input |
| `Number` | Numeric input |
| `Select` | Single selection from a list of options |
| `MultiSelect` | Multiple selections from a list of options |
| `Checkbox` | Boolean true/false |
| `Date` | Date picker |

## Select and MultiSelect options

For `Select` and `MultiSelect` fields, provide an `options` array:

```json
{
  "name": "preferred_staff",
  "label": "Preferred Staff Member",
  "type": "Select",
  "required": false,
  "options": ["Alice", "Bob", "Charlie"]
}
```

## Validation

- **Required fields:** If a required field is missing or empty, a `422 Unprocessable Entity` response is returned.
- **Type validation:** Field values are validated against their declared type.
- Validation failures return a `CustomFieldValidationException` error.

## Submitting custom field values

Pass values in the booking creation request:

```json
{
  "bookingTypeId": "3fa85f64-...",
  "startTime": "2026-04-15T09:00:00Z",
  "customerName": "Jane Doe",
  "customerEmail": "jane@example.com",
  "customFields": {
    "pet_name": "Fluffy",
    "preferred_staff": "Alice"
  }
}
```
