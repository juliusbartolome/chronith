# Public Booking Contact Fields + Customer Refactor — Design Document

**Date:** 2026-03-24
**Status:** Approved

---

## 1. Problem Statement

Public bookings currently capture only `CustomerId` (free-form string) and `CustomerEmail`. There are no name or mobile fields. Tenants need to collect customer contact information (first name, last name, mobile number) during the public booking flow, and this data should be linked back to the Customer record system.

Additionally, the Customer model uses a single `Name` field and `Phone`, which should be refactored to `FirstName`/`LastName` and `Mobile` for consistency.

## 2. Solution Overview

1. Add `FirstName`, `LastName`, `Mobile` as optional fields on the **Booking** domain model.
2. Refactor **Customer** model: `Name` → `FirstName` + `LastName`, `Phone` → `Mobile`.
3. Public booking endpoint accepts optional `FirstName`, `LastName`, `Mobile`.
4. On public booking creation, upsert a Customer record by email (create if not found, update if found).
5. Link the Booking to the Customer via the existing `CustomerAccountId` FK.

## 3. Design Decisions

| Decision                           | Choice                                                                                                               | Rationale                                                           |
| ---------------------------------- | -------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------- |
| Fields on Booking                  | First-class domain fields (not CustomFields JSON)                                                                    | Queryable, typed, validated                                         |
| Customer Name                      | Split `Name` → `FirstName` + `LastName` on both Booking and Customer                                                 | Full consistency across models                                      |
| Customer Phone                     | Rename `Phone` → `Mobile` everywhere                                                                                 | Consistent with Booking.Mobile                                      |
| OIDC name splitting                | Split on first space                                                                                                 | `"Julius Bartolome"` → FirstName=`"Julius"`, LastName=`"Bartolome"` |
| Notification templates             | Keep `{{customer_name}}` as computed, add `{{customer_first_name}}`, `{{customer_last_name}}`, `{{customer_mobile}}` | Non-breaking for existing templates                                 |
| API versioning                     | Breaking change on `/v1/`                                                                                            | Pre-production, no external consumers                               |
| DB migration data                  | Split existing `name` on first space                                                                                 | Best-effort data preservation                                       |
| Customer upsert                    | Create if not found, update if found                                                                                 | Automatic customer record creation from public bookings             |
| Auto-created customer AuthProvider | `"public"`                                                                                                           | Distinguishes from `"builtin"` (registered) and OIDC                |

## 4. Domain Model Changes

### 4.1 Booking

Add three optional fields:

```csharp
public string FirstName { get; private set; } = string.Empty;
public string LastName { get; private set; } = string.Empty;
public string? Mobile { get; private set; }
```

Update `Booking.Create()` factory to accept optional `firstName`, `lastName`, `mobile` parameters.

### 4.2 Customer

Replace existing fields:

```csharp
// Before
public string Name { get; private set; } = string.Empty;
public string? Phone { get; private set; }

// After
public string FirstName { get; private set; } = string.Empty;
public string LastName { get; private set; } = string.Empty;
public string? Mobile { get; private set; }
```

Update factory methods:

- `Create(tenantId, email, passwordHash, firstName, lastName, mobile, authProvider)`
- `CreateOidc(tenantId, email, firstName, lastName, externalId, authProvider)` — split name claim on first space
- `Hydrate(...)` — updated signature
- `UpdateProfile(firstName, lastName, mobile)`

## 5. Public Booking Flow

### 5.1 Endpoint Request

```
POST /v1/public/{tenantSlug}/booking-types/{slug}/bookings
{
  "startTime": "...",
  "customerEmail": "...",
  "customerId": "...",
  "firstName": "Julius",      // optional
  "lastName": "Bartolome",    // optional
  "mobile": "+639171234567"   // optional
}
```

### 5.2 Command Handler — Customer Upsert

After creating the Booking:

1. Look up Customer by email in the tenant (`ICustomerRepository.GetByEmailAsync`)
2. **If found:** update `FirstName`, `LastName`, `Mobile` (only if provided/non-empty)
3. **If not found:** create a new Customer:
   - `AuthProvider = "public"`
   - No password (null PasswordHash)
   - `IsEmailVerified = false`
   - `IsActive = true`
4. Set `Booking.CustomerAccountId` to the Customer's ID

## 6. Infrastructure Changes

### 6.1 Booking Entity

Add columns: `first_name` (string, required, default empty), `last_name` (string, required, default empty), `mobile` (string, nullable).

### 6.2 Customer Entity

- `Name` → `FirstName` + `LastName`
- `Phone` → `Mobile`
- `PhoneEncrypted` → `MobileEncrypted`

### 6.3 Database Migration

```sql
-- Customer table
ALTER TABLE chronith.customers RENAME COLUMN phone TO mobile;
ALTER TABLE chronith.customers RENAME COLUMN phone_encrypted TO mobile_encrypted;
ALTER TABLE chronith.customers ADD COLUMN last_name text NOT NULL DEFAULT '';
-- Split name → first_name + last_name
ALTER TABLE chronith.customers RENAME COLUMN name TO first_name;
UPDATE chronith.customers SET
  first_name = SPLIT_PART(first_name, ' ', 1),
  last_name = CASE
    WHEN POSITION(' ' IN first_name) > 0
    THEN SUBSTRING(first_name FROM POSITION(' ' IN first_name) + 1)
    ELSE ''
  END
WHERE first_name != '';

-- Booking table
ALTER TABLE chronith.bookings ADD COLUMN first_name text NOT NULL DEFAULT '';
ALTER TABLE chronith.bookings ADD COLUMN last_name text NOT NULL DEFAULT '';
ALTER TABLE chronith.bookings ADD COLUMN mobile text;
```

Note: The actual migration will be generated via EF Core tooling. The SQL above is illustrative.

## 7. Application Layer Changes

### 7.1 DTOs

- `BookingDto`: add `FirstName`, `LastName`, `Mobile`
- `CustomerDto`: replace `Name` → `FirstName` + `LastName`, `Phone` → `Mobile`

### 7.2 Commands

- `PublicCreateBookingCommand`: add optional `FirstName`, `LastName`, `Mobile`
- `CustomerRegisterCommand`: replace `Name` → `FirstName` + `LastName`, `Phone` → `Mobile`
- `CustomerOidcLoginCommand`: handler splits name claim on first space
- `UpdateCustomerProfileCommand`: replace `Name` → `FirstName` + `LastName`, `Phone` → `Mobile`

### 7.3 Notification Templates

- Keep `{{customer_name}}` as computed: `$"{FirstName} {LastName}".Trim()`
- Add `{{customer_first_name}}`, `{{customer_last_name}}`, `{{customer_mobile}}`

## 8. Booking Domain Model — CustomerAccountId

The `Booking` domain model currently does not have a `CustomerAccountId` property (it only exists on the infrastructure entity). We need to add it:

```csharp
public Guid? CustomerAccountId { get; private set; }

public void LinkCustomerAccount(Guid customerAccountId)
{
    CustomerAccountId = customerAccountId;
}
```

## 9. Test Impact

All test layers need updates:

- **Unit tests:** Customer and Booking model tests, builder updates
- **Integration tests:** Customer repository tests
- **Functional tests:** Public booking endpoints, customer auth endpoints, seed data, test constants
- **Dashboard:** Customer display components

## 10. Files Affected (Estimated)

| Layer          | Files | Description                                        |
| -------------- | ----- | -------------------------------------------------- |
| Domain         | 2     | Booking.cs, Customer.cs                            |
| Application    | ~10   | Commands, DTOs, mappers, validators                |
| Infrastructure | ~8    | Entities, configurations, mappers, repositories    |
| API            | ~5    | Public booking endpoint, customer endpoints        |
| Tests          | ~15   | Unit, integration, functional, builders, seed data |
| Dashboard      | ~5    | Customer-related components                        |

**Total: ~45 files**
