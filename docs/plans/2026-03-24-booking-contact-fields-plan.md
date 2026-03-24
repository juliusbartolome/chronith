# Booking Contact Fields + Customer Refactor Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add FirstName, LastName, Mobile to Booking and Customer models, with customer upsert on public booking creation.

**Architecture:** Refactor Customer.Name → FirstName + LastName, Customer.Phone → Mobile. Add same fields to Booking (optional). Public booking endpoint upserts Customer by email and links via CustomerAccountId FK.

**Tech Stack:** .NET 10, EF Core 10, FastEndpoints, MediatR, FluentValidation, xUnit, FluentAssertions, NSubstitute

---

### Task 1: Refactor Customer Domain Model

**Files:**

- Modify: `src/Chronith.Domain/Models/Customer.cs`
- Test: `tests/Chronith.Tests.Unit/Domain/CustomerTests.cs` (create if needed)

**Step 1: Write failing tests for Customer with FirstName/LastName/Mobile**

Create `tests/Chronith.Tests.Unit/Domain/CustomerTests.cs`:

```csharp
using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class CustomerTests
{
    [Fact]
    public void Create_SetsFirstNameLastNameMobile()
    {
        var customer = Customer.Create(
            Guid.NewGuid(), "test@example.com", "hash",
            "Julius", "Bartolome", "+639171234567", "builtin");

        customer.FirstName.Should().Be("Julius");
        customer.LastName.Should().Be("Bartolome");
        customer.Mobile.Should().Be("+639171234567");
    }

    [Fact]
    public void CreateOidc_SplitsNameOnFirstSpace()
    {
        var customer = Customer.CreateOidc(
            Guid.NewGuid(), "test@example.com", "Julius Bartolome",
            "ext-123", "google");

        customer.FirstName.Should().Be("Julius");
        customer.LastName.Should().Be("Bartolome");
    }

    [Fact]
    public void CreateOidc_SingleName_PutsAllInFirstName()
    {
        var customer = Customer.CreateOidc(
            Guid.NewGuid(), "test@example.com", "Madonna",
            "ext-456", "google");

        customer.FirstName.Should().Be("Madonna");
        customer.LastName.Should().Be(string.Empty);
    }

    [Fact]
    public void UpdateProfile_SetsFirstNameLastNameMobile()
    {
        var customer = Customer.Create(
            Guid.NewGuid(), "test@example.com", "hash",
            "Old", "Name", null, "builtin");

        customer.UpdateProfile("New", "Name", "+639170000000");

        customer.FirstName.Should().Be("New");
        customer.LastName.Should().Be("Name");
        customer.Mobile.Should().Be("+639170000000");
    }

    [Fact]
    public void Hydrate_SetsAllFields()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var customer = Customer.Hydrate(
            id, tenantId, "test@example.com", null,
            "Julius", "Bartolome", "+639171234567",
            null, "builtin", true, true, false,
            DateTimeOffset.UtcNow, null, 0);

        customer.Id.Should().Be(id);
        customer.FirstName.Should().Be("Julius");
        customer.LastName.Should().Be("Bartolome");
        customer.Mobile.Should().Be("+639171234567");
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~CustomerTests" --no-restore`
Expected: Compilation errors — `Customer.Create` does not accept FirstName/LastName/Mobile yet.

**Step 3: Implement Customer domain model changes**

Modify `src/Chronith.Domain/Models/Customer.cs`:

Replace:

```csharp
public string Name { get; private set; } = string.Empty;
public string? Phone { get; private set; }
```

With:

```csharp
public string FirstName { get; private set; } = string.Empty;
public string LastName { get; private set; } = string.Empty;
public string? Mobile { get; private set; }
```

Update `Create`:

```csharp
public static Customer Create(Guid tenantId, string email, string? passwordHash, string firstName,
    string lastName, string? mobile, string authProvider)
{
    return new Customer
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Email = email,
        PasswordHash = passwordHash,
        FirstName = firstName,
        LastName = lastName,
        Mobile = mobile,
        AuthProvider = authProvider,
        IsEmailVerified = false,
        IsActive = true,
        IsDeleted = false,
        CreatedAt = DateTimeOffset.UtcNow
    };
}
```

Update `CreateOidc` — split name on first space:

```csharp
public static Customer CreateOidc(Guid tenantId, string email, string name, string externalId,
    string authProvider)
{
    var spaceIndex = (name ?? string.Empty).IndexOf(' ');
    var firstName = spaceIndex > 0 ? name![..spaceIndex] : name ?? string.Empty;
    var lastName = spaceIndex > 0 ? name![( spaceIndex + 1)..] : string.Empty;

    return new Customer
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Email = email,
        PasswordHash = null,
        FirstName = firstName,
        LastName = lastName,
        ExternalId = externalId,
        AuthProvider = authProvider,
        IsEmailVerified = true,
        IsActive = true,
        IsDeleted = false,
        CreatedAt = DateTimeOffset.UtcNow
    };
}
```

Update `Hydrate`:

```csharp
internal static Customer Hydrate(Guid id, Guid tenantId, string email, string? passwordHash,
    string firstName, string lastName, string? mobile, string? externalId, string authProvider,
    bool isEmailVerified, bool isActive, bool isDeleted, DateTimeOffset createdAt,
    DateTimeOffset? lastLoginAt, uint rowVersion) => new()
{
    Id = id, TenantId = tenantId, Email = email, PasswordHash = passwordHash,
    FirstName = firstName, LastName = lastName, Mobile = mobile,
    ExternalId = externalId, AuthProvider = authProvider,
    IsEmailVerified = isEmailVerified, IsActive = isActive, IsDeleted = isDeleted,
    CreatedAt = createdAt, LastLoginAt = lastLoginAt, RowVersion = rowVersion
};
```

Update `UpdateProfile`:

```csharp
public void UpdateProfile(string firstName, string lastName, string? mobile)
{
    FirstName = firstName;
    LastName = lastName;
    Mobile = mobile;
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~CustomerTests" --no-restore`
Expected: All 5 tests PASS.

**Step 5: Commit**

```bash
git add -A && git commit -m "feat(domain): refactor Customer Name→FirstName+LastName, Phone→Mobile"
```

---

### Task 2: Add Contact Fields to Booking Domain Model

**Files:**

- Modify: `src/Chronith.Domain/Models/Booking.cs`
- Modify: `tests/Chronith.Tests.Unit/Helpers/BookingBuilder.cs`

**Step 1: Write failing test for Booking with FirstName/LastName/Mobile**

Add to existing booking tests or create `tests/Chronith.Tests.Unit/Domain/BookingContactFieldsTests.cs`:

```csharp
using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class BookingContactFieldsTests
{
    [Fact]
    public void Create_WithContactFields_SetsValues()
    {
        var booking = Booking.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
            "cust-1", "test@example.com",
            10000, "PHP",
            firstName: "Julius", lastName: "Bartolome", mobile: "+639171234567");

        booking.FirstName.Should().Be("Julius");
        booking.LastName.Should().Be("Bartolome");
        booking.Mobile.Should().Be("+639171234567");
    }

    [Fact]
    public void Create_WithoutContactFields_DefaultsToEmpty()
    {
        var booking = Booking.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
            "cust-1", "test@example.com",
            10000, "PHP");

        booking.FirstName.Should().Be(string.Empty);
        booking.LastName.Should().Be(string.Empty);
        booking.Mobile.Should().BeNull();
    }

    [Fact]
    public void LinkCustomerAccount_SetsCustomerAccountId()
    {
        var booking = Booking.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1),
            "cust-1", "test@example.com",
            10000, "PHP");

        var customerId = Guid.NewGuid();
        booking.LinkCustomerAccount(customerId);

        booking.CustomerAccountId.Should().Be(customerId);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~BookingContactFieldsTests" --no-restore`
Expected: Compilation errors — properties don't exist.

**Step 3: Implement Booking domain model changes**

Modify `src/Chronith.Domain/Models/Booking.cs`:

Add properties (after `CustomerEmail`, line 14):

```csharp
public string FirstName { get; private set; } = string.Empty;
public string LastName { get; private set; } = string.Empty;
public string? Mobile { get; private set; }
public Guid? CustomerAccountId { get; private set; }
```

Update `Create` factory — add optional parameters:

```csharp
public static Booking Create(
    Guid tenantId,
    Guid bookingTypeId,
    DateTimeOffset start,
    DateTimeOffset end,
    string customerId,
    string customerEmail,
    long amountInCentavos,
    string currency,
    string? paymentReference = null,
    string? customFields = null,
    string? firstName = null,
    string? lastName = null,
    string? mobile = null)
{
    var isFree = amountInCentavos == 0;
    return new Booking
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        BookingTypeId = bookingTypeId,
        Start = start,
        End = end,
        Status = isFree ? BookingStatus.PendingVerification : BookingStatus.PendingPayment,
        CustomerId = customerId,
        CustomerEmail = customerEmail,
        AmountInCentavos = amountInCentavos,
        Currency = currency,
        PaymentReference = paymentReference,
        CustomFields = customFields,
        FirstName = firstName ?? string.Empty,
        LastName = lastName ?? string.Empty,
        Mobile = mobile
    };
}
```

Add `LinkCustomerAccount` method:

```csharp
public void LinkCustomerAccount(Guid customerAccountId)
{
    CustomerAccountId = customerAccountId;
}
```

Update `BookingBuilder` in `tests/Chronith.Tests.Unit/Helpers/BookingBuilder.cs`:

- Add `_firstName`, `_lastName`, `_mobile` fields with defaults
- Add `WithFirstName`, `WithLastName`, `WithMobile` builder methods
- Pass them to `Booking.Create(...)` in the `Build()` method

**Step 4: Run tests**

Run: `dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~BookingContactFieldsTests" --no-restore`
Expected: PASS

**Step 5: Commit**

```bash
git add -A && git commit -m "feat(domain): add FirstName, LastName, Mobile, CustomerAccountId to Booking"
```

---

### Task 3: Update Customer Infrastructure (Entity, Configuration, Mapper, Repository)

**Files:**

- Modify: `src/Chronith.Infrastructure/Persistence/Entities/CustomerEntity.cs`
- Modify: `src/Chronith.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs`
- Modify: `src/Chronith.Infrastructure/Persistence/Mappers/CustomerEntityMapper.cs`
- Modify: `src/Chronith.Infrastructure/Persistence/Repositories/CustomerRepository.cs`

**Step 1: Update CustomerEntity**

Replace `Name` and `Phone`/`PhoneEncrypted` with:

```csharp
public string FirstName { get; set; } = string.Empty;
public string LastName { get; set; } = string.Empty;
public string? Mobile { get; set; }
/// <summary>AES-256-GCM ciphertext of Mobile. Nullable — same as Mobile.</summary>
public string? MobileEncrypted { get; set; }
```

**Step 2: Update CustomerConfiguration**

Replace:

```csharp
builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
builder.Property(c => c.Phone).HasMaxLength(50);
builder.Property(c => c.PhoneEncrypted);
```

With:

```csharp
builder.Property(c => c.FirstName).IsRequired().HasMaxLength(200);
builder.Property(c => c.LastName).IsRequired().HasMaxLength(200);
builder.Property(c => c.Mobile).HasMaxLength(50);
builder.Property(c => c.MobileEncrypted);
```

**Step 3: Update CustomerEntityMapper**

`ToEntity`:

```csharp
public static CustomerEntity ToEntity(this Customer c) => new()
{
    Id = c.Id,
    TenantId = c.TenantId,
    Email = c.Email,
    PasswordHash = c.PasswordHash,
    FirstName = c.FirstName,
    LastName = c.LastName,
    Mobile = c.Mobile,
    ExternalId = c.ExternalId,
    AuthProvider = c.AuthProvider,
    IsEmailVerified = c.IsEmailVerified,
    IsActive = c.IsActive,
    IsDeleted = c.IsDeleted,
    CreatedAt = c.CreatedAt,
    LastLoginAt = c.LastLoginAt,
    RowVersion = c.RowVersion
};
```

`ToDomain`:

```csharp
public static Customer ToDomain(this CustomerEntity e) =>
    Customer.Hydrate(
        e.Id, e.TenantId, e.Email, e.PasswordHash,
        e.FirstName, e.LastName, e.Mobile, e.ExternalId, e.AuthProvider,
        e.IsEmailVerified, e.IsActive, e.IsDeleted,
        e.CreatedAt, e.LastLoginAt, e.RowVersion);
```

**Step 4: Update CustomerRepository**

Rename `DecryptPhone` to `DecryptMobile` (same logic). Update `MapToDomain`:

```csharp
private Customer MapToDomain(Entities.CustomerEntity e)
{
    e.Email = e.EmailEncrypted is not null ? DecryptEmail(e.EmailEncrypted) : e.Email;
    if (e.MobileEncrypted is not null)
        e.Mobile = DecryptMobile(e.MobileEncrypted);
    return e.ToDomain();
}
```

In `AddAsync` and `Update`, replace phone encryption with mobile encryption:

- `entity.PhoneEncrypted = ...` → `entity.MobileEncrypted = ...`
- References to `Phone` → `Mobile`

**Step 5: Verify build compiles**

Run: `dotnet build src/Chronith.Infrastructure --no-restore`
Expected: Compilation errors from callers of Customer.Create/Hydrate — these will be fixed in subsequent tasks.

**Step 6: Commit**

```bash
git add -A && git commit -m "refactor(infra): update Customer entity, config, mapper, repository for FirstName/LastName/Mobile"
```

---

### Task 4: Update Booking Infrastructure (Entity, Configuration, Mapper)

**Files:**

- Modify: `src/Chronith.Infrastructure/Persistence/Entities/BookingEntity.cs`
- Modify: `src/Chronith.Infrastructure/Persistence/Configurations/BookingConfiguration.cs`
- Modify: `src/Chronith.Infrastructure/Persistence/Mappers/BookingEntityMapper.cs`

**Step 1: Update BookingEntity**

Add after `CustomerEmail` (line 14):

```csharp
public string FirstName { get; set; } = string.Empty;
public string LastName { get; set; } = string.Empty;
public string? Mobile { get; set; }
```

**Step 2: Update BookingConfiguration**

Add after `CustomerEmail` config:

```csharp
builder.Property(b => b.FirstName)
    .IsRequired()
    .HasMaxLength(200)
    .HasDefaultValue(string.Empty);

builder.Property(b => b.LastName)
    .IsRequired()
    .HasMaxLength(200)
    .HasDefaultValue(string.Empty);

builder.Property(b => b.Mobile)
    .HasMaxLength(50);
```

**Step 3: Update BookingEntityMapper**

In `ToDomain`, add:

```csharp
SetPrivate(domain, nameof(Booking.FirstName), entity.FirstName);
SetPrivate(domain, nameof(Booking.LastName), entity.LastName);
SetPrivate(domain, nameof(Booking.Mobile), entity.Mobile);
SetPrivate(domain, nameof(Booking.CustomerAccountId), entity.CustomerAccountId);
```

In `ToEntity`, add:

```csharp
FirstName = domain.FirstName,
LastName = domain.LastName,
Mobile = domain.Mobile,
CustomerAccountId = domain.CustomerAccountId,
```

**Step 4: Commit**

```bash
git add -A && git commit -m "feat(infra): add FirstName, LastName, Mobile to Booking entity and mapper"
```

---

### Task 5: Update Application Layer (DTOs, Mappers, Commands)

**Files:**

- Modify: `src/Chronith.Application/DTOs/BookingDto.cs`
- Modify: `src/Chronith.Application/DTOs/CustomerDto.cs`
- Modify: `src/Chronith.Application/Mappers/BookingMapper.cs`
- Modify: `src/Chronith.Application/Mappers/CustomerMapper.cs`
- Modify: `src/Chronith.Application/Commands/CustomerAuth/Register/CustomerRegisterCommand.cs`
- Modify: `src/Chronith.Application/Commands/CustomerAuth/OidcLogin/CustomerOidcLoginCommand.cs`
- Modify: `src/Chronith.Application/Commands/CustomerAuth/UpdateProfile/UpdateCustomerProfileCommand.cs`
- Modify: `src/Chronith.Application/Commands/CustomerAuth/MagicLink/CustomerMagicLinkRegisterCommand.cs`
- Modify: `src/Chronith.Application/Commands/Public/PublicCreateBookingCommand.cs`

**Step 1: Update DTOs**

`CustomerDto`:

```csharp
public sealed record CustomerDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Mobile,
    string AuthProvider,
    bool IsEmailVerified,
    DateTimeOffset CreatedAt);
```

`BookingDto` — add after `CustomerEmail`:

```csharp
public sealed record BookingDto(
    Guid Id,
    Guid BookingTypeId,
    DateTimeOffset Start,
    DateTimeOffset End,
    BookingStatus Status,
    string CustomerId,
    string CustomerEmail,
    string FirstName,
    string LastName,
    string? Mobile,
    string? PaymentReference,
    long AmountInCentavos,
    string Currency,
    string? CheckoutUrl,
    Guid? StaffMemberId,
    IReadOnlyList<BookingStatusChangeDto> StatusChanges,
    string? PaymentUrl = null
);
```

**Step 2: Update Mappers**

`CustomerMapper`:

```csharp
public static CustomerDto ToDto(this Customer customer) =>
    new(customer.Id, customer.Email, customer.FirstName, customer.LastName,
        customer.Mobile, customer.AuthProvider, customer.IsEmailVerified, customer.CreatedAt);
```

`BookingMapper` — add `FirstName`, `LastName`, `Mobile` to both `ToDto` overloads.

**Step 3: Update Customer Commands**

`CustomerRegisterCommand` — replace `Name`/`Phone` with `FirstName`/`LastName`/`Mobile`:

- Command record: `public required string FirstName { get; init; }`, `public required string LastName { get; init; }`, `public string? Mobile { get; init; }`
- Validator: `RuleFor(x => x.FirstName).NotEmpty().MaximumLength(200);`, `RuleFor(x => x.LastName).NotEmpty().MaximumLength(200);`
- Handler: `Customer.Create(tenant.Id, request.Email, passwordHash, request.FirstName, request.LastName, request.Mobile, "builtin")`

`CustomerOidcLoginCommand` handler — `Customer.CreateOidc` signature unchanged (still accepts `name` string, splits internally).

`UpdateCustomerProfileCommand` — replace `Name`/`Phone`:

- Command: `public required string FirstName { get; init; }`, `public required string LastName { get; init; }`, `public string? Mobile { get; init; }`
- Validator: `RuleFor(x => x.FirstName).NotEmpty().MaximumLength(200);`, `RuleFor(x => x.LastName).NotEmpty().MaximumLength(200);`
- Handler: `customer.UpdateProfile(request.FirstName, request.LastName, request.Mobile)`

`CustomerMagicLinkRegisterCommand` — replace `Name`/`Phone`:

- Command: `public required string FirstName { get; init; }`, `public required string LastName { get; init; }`, `public string? Mobile { get; init; }`
- Validator: `RuleFor(x => x.FirstName).NotEmpty().MaximumLength(200);`, `RuleFor(x => x.LastName).NotEmpty().MaximumLength(200);`
- Handler: `Customer.Create(tenant.Id, request.Email, passwordHash: null, request.FirstName, request.LastName, request.Mobile, authProvider: "magic-link")`

**Step 4: Update PublicCreateBookingCommand**

Add optional fields to command record:

```csharp
public string? FirstName { get; init; }
public string? LastName { get; init; }
public string? Mobile { get; init; }
```

Add validation rules (optional but with max length):

```csharp
RuleFor(x => x.FirstName).MaximumLength(200).When(x => x.FirstName is not null);
RuleFor(x => x.LastName).MaximumLength(200).When(x => x.LastName is not null);
RuleFor(x => x.Mobile).MaximumLength(50).When(x => x.Mobile is not null);
```

Pass to `Booking.Create`:

```csharp
var booking = Booking.Create(
    cmd.TenantId,
    bookingType.Id,
    start, end,
    cmd.CustomerId,
    cmd.CustomerEmail,
    amountInCentavos: bookingType.PriceInCentavos,
    currency: bookingType.Currency,
    firstName: cmd.FirstName,
    lastName: cmd.LastName,
    mobile: cmd.Mobile);
```

**Step 5: Commit**

```bash
git add -A && git commit -m "feat(app): update DTOs, mappers, commands for FirstName/LastName/Mobile"
```

---

### Task 6: Add Customer Upsert Logic to PublicCreateBookingHandler

**Files:**

- Modify: `src/Chronith.Application/Commands/Public/PublicCreateBookingCommand.cs`
- Test: `tests/Chronith.Tests.Unit/Application/PublicCreateBookingHandlerTests.cs`

**Step 1: Write failing tests for customer upsert**

Add tests to `tests/Chronith.Tests.Unit/Application/PublicCreateBookingHandlerTests.cs`:

1. `PublicCreateBooking_WithContactFields_CreatesNewCustomer` — when no customer exists for the email, handler creates one with AuthProvider="public" and links booking.
2. `PublicCreateBooking_WithContactFields_UpdatesExistingCustomer` — when customer already exists, handler updates FirstName/LastName/Mobile and links booking.
3. `PublicCreateBooking_WithoutContactFields_SkipsCustomerUpsert` — when FirstName/LastName/Mobile are all null, handler does not touch the customer repository.

**Step 2: Run tests to verify they fail**

**Step 3: Implement customer upsert in handler**

Add `ICustomerRepository customerRepo` to the handler's primary constructor.

After `await bookingRepo.AddAsync(booking, ct);` and before `await tx.CommitAsync(ct);`:

```csharp
// Customer upsert — only when contact fields are provided
if (!string.IsNullOrWhiteSpace(cmd.FirstName) || !string.IsNullOrWhiteSpace(cmd.LastName) || !string.IsNullOrWhiteSpace(cmd.Mobile))
{
    var existingCustomer = await customerRepo.GetByEmailAsync(cmd.TenantId, cmd.CustomerEmail, ct);
    if (existingCustomer is not null)
    {
        if (!string.IsNullOrWhiteSpace(cmd.FirstName))
            existingCustomer.UpdateProfile(
                cmd.FirstName ?? existingCustomer.FirstName,
                cmd.LastName ?? existingCustomer.LastName,
                cmd.Mobile ?? existingCustomer.Mobile);
        customerRepo.Update(existingCustomer);
        booking.LinkCustomerAccount(existingCustomer.Id);
    }
    else
    {
        var newCustomer = Customer.Create(
            cmd.TenantId,
            cmd.CustomerEmail,
            passwordHash: null,
            cmd.FirstName ?? string.Empty,
            cmd.LastName ?? string.Empty,
            cmd.Mobile,
            authProvider: "public");
        await customerRepo.AddAsync(newCustomer, ct);
        booking.LinkCustomerAccount(newCustomer.Id);
    }
}
```

**Step 4: Run tests**

Run: `dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~PublicCreateBookingHandler" --no-restore`
Expected: PASS

**Step 5: Commit**

```bash
git add -A && git commit -m "feat(app): add customer upsert logic to PublicCreateBookingHandler"
```

---

### Task 7: Update API Endpoints

**Files:**

- Modify: `src/Chronith.API/Endpoints/Public/PublicCreateBookingEndpoint.cs`
- Modify: `src/Chronith.API/Endpoints/Public/CustomerAuth/CustomerRegisterEndpoint.cs`
- Modify: `src/Chronith.API/Endpoints/Public/CustomerAuth/CustomerMagicLinkRegisterEndpoint.cs`
- Modify: `src/Chronith.API/Endpoints/Public/CustomerAuth/UpdateCustomerMeEndpoint.cs`

**Step 1: Update PublicCreateBookingRequest**

Add optional fields:

```csharp
public string? FirstName { get; set; }
public string? LastName { get; set; }
public string? Mobile { get; set; }
```

Pass to command in `HandleAsync`:

```csharp
var result = await sender.Send(new PublicCreateBookingCommand
{
    TenantId = tenant.Id,
    BookingTypeSlug = req.Slug,
    StartTime = req.StartTime,
    CustomerEmail = req.CustomerEmail,
    CustomerId = req.CustomerId,
    FirstName = req.FirstName,
    LastName = req.LastName,
    Mobile = req.Mobile
}, ct);
```

**Step 2: Update CustomerRegisterRequest**

Replace `Name`/`Phone` with `FirstName`/`LastName`/`Mobile`. Update handler to pass new fields.

**Step 3: Update CustomerMagicLinkRegisterRequest**

Replace `Name`/`Phone` with `FirstName`/`LastName`/`Mobile`. Update handler to pass new fields.

**Step 4: Update UpdateCustomerMeRequest**

Replace `Name`/`Phone` with `FirstName`/`LastName`/`Mobile`. Update handler to pass new fields.

**Step 5: Commit**

```bash
git add -A && git commit -m "feat(api): update endpoints for FirstName/LastName/Mobile"
```

---

### Task 8: Update Notification System

**Files:**

- Modify: `src/Chronith.Infrastructure/Services/NotificationDispatcherService.cs`
- Modify: `src/Chronith.Application/Notifications/NotificationOutboxHandler.cs`

**Step 1: Update NotificationPayload**

The `NotificationPayload` record in `NotificationOutboxHandler.cs` does not currently have a `customerName` field — `NotificationDispatcherService` tries to read it from JSON but falls back to `customerEmail`. We need to add `CustomerFirstName` and `CustomerLastName` to the payload.

Update the `NotificationPayload` record:

```csharp
file sealed record NotificationPayload(
    string Event,
    Guid BookingId,
    Guid TenantId,
    string BookingTypeSlug,
    string Status,
    DateTimeOffset Start,
    DateTimeOffset End,
    string CustomerId,
    string CustomerEmail,
    string CustomerFirstName,
    string CustomerLastName,
    string? CustomerMobile,
    DateTimeOffset OccurredAt);
```

Update the payload construction in the handler to pass the new fields. This requires adding `CustomerFirstName`, `CustomerLastName`, `CustomerMobile` to `BookingStatusChangedNotification`.

Update `BookingStatusChangedNotification` in `src/Chronith.Application/Notifications/BookingStatusChangedNotification.cs`:

```csharp
public sealed record BookingStatusChangedNotification(
    Guid BookingId,
    Guid TenantId,
    Guid BookingTypeId,
    string BookingTypeSlug,
    BookingStatus? FromStatus,
    BookingStatus ToStatus,
    DateTimeOffset Start,
    DateTimeOffset End,
    string CustomerId,
    string CustomerEmail,
    string CustomerFirstName = "",
    string CustomerLastName = "",
    string? CustomerMobile = null) : INotification;
```

**Step 2: Update NotificationDispatcherService context**

In `NotificationDispatcherService.cs`, update the context dictionary:

```csharp
var context = new Dictionary<string, string>
{
    ["customer_name"] = $"{TryGetStringProperty(payload, "customerFirstName")} {TryGetStringProperty(payload, "customerLastName")}".Trim(),
    ["customer_first_name"] = TryGetStringProperty(payload, "customerFirstName") ?? string.Empty,
    ["customer_last_name"] = TryGetStringProperty(payload, "customerLastName") ?? string.Empty,
    ["customer_mobile"] = TryGetStringProperty(payload, "customerMobile") ?? string.Empty,
    ["customer_email"] = customerEmail,
    // ... rest unchanged
};
```

Fallback: if both first/last are empty, `customer_name` falls back to email (existing behavior).

**Step 3: Update all callers of BookingStatusChangedNotification**

Search for all places that publish `BookingStatusChangedNotification` and add the new fields. These are in:

- `PublicCreateBookingCommand.cs` handler
- `CreateBookingCommand.cs` handler
- `ProcessPaymentWebhookCommand.cs` handler
- `PayBookingCommand.cs` handler
- `CancelBookingCommand.cs` handler
- `ConfirmBookingCommand.cs` handler
- `RecurringBookingGeneratorService.cs`

For each, add:

```csharp
CustomerFirstName: booking.FirstName,
CustomerLastName: booking.LastName,
CustomerMobile: booking.Mobile
```

**Step 4: Commit**

```bash
git add -A && git commit -m "feat(infra): update notification system for FirstName/LastName/Mobile template vars"
```

---

### Task 9: Generate EF Core Migration

**Files:**

- Create: `src/Chronith.Infrastructure/Migrations/PostgreSQL/<migration>.cs`

**Step 1: Generate migration**

```bash
dotnet ef migrations add AddBookingContactFieldsRefactorCustomerNames \
  --project src/Chronith.Infrastructure \
  --startup-project src/Chronith.API \
  --output-dir Migrations/PostgreSQL
```

**Step 2: Review generated migration**

The migration should:

1. Add `FirstName`, `LastName`, `Mobile` columns to `bookings` table
2. Rename `Name` → `FirstName` on `customers` table, add `LastName` column
3. Rename `Phone` → `Mobile`, `PhoneEncrypted` → `MobileEncrypted` on `customers` table

**Step 3: Add data migration SQL to split existing Customer.Name**

In the migration `Up` method, after the column renames, add:

```csharp
migrationBuilder.Sql("""
    UPDATE chronith.customers SET
      "LastName" = CASE
        WHEN POSITION(' ' IN "FirstName") > 0
        THEN SUBSTRING("FirstName" FROM POSITION(' ' IN "FirstName") + 1)
        ELSE ''
      END,
      "FirstName" = CASE
        WHEN POSITION(' ' IN "FirstName") > 0
        THEN SUBSTRING("FirstName" FROM 1 FOR POSITION(' ' IN "FirstName") - 1)
        ELSE "FirstName"
      END
    WHERE "FirstName" != '';
""");
```

**Step 4: Commit**

```bash
git add -A && git commit -m "feat(infra): add EF migration for booking contact fields and customer name refactor"
```

---

### Task 10: Fix All Remaining Compilation Errors and Unit Tests

**Files:**

- Various test files across all test projects
- `tests/Chronith.Tests.Unit/Application/` — various handler tests
- `tests/Chronith.Tests.Unit/Infrastructure/` — notification tests

**Step 1: Build entire solution**

```bash
dotnet build Chronith.slnx
```

Fix all compilation errors — these will primarily be:

- Test files that call `Customer.Create` with old signature (Name, Phone)
- Test files that construct `BookingStatusChangedNotification` without new fields
- `BookingBuilder` callers that may need updates
- `SeedData.SeedCustomerAsync` using `Name =` property
- Any other references to `customer.Name`, `customer.Phone`

**Step 2: Run unit tests**

```bash
dotnet test tests/Chronith.Tests.Unit
```

Fix any failures.

**Step 3: Commit**

```bash
git add -A && git commit -m "fix: resolve all compilation errors and update unit tests for name refactor"
```

---

### Task 11: Update Functional Tests and Seed Data

**Files:**

- Modify: `tests/Chronith.Tests.Functional/Helpers/SeedData.cs`
- Modify: `tests/Chronith.Tests.Functional/Public/PublicBookingEndpointsTests.cs`
- Modify: Various functional test files

**Step 1: Update SeedData.SeedCustomerAsync**

Replace `Name = name` with `FirstName = firstName, LastName = lastName`. Update method signature to accept `firstName` and `lastName` instead of `name`.

**Step 2: Add functional test for public booking with contact fields**

In `PublicBookingEndpointsTests.cs`, add a test:

```csharp
[Fact]
public async Task PublicCreateBooking_WithContactFields_Creates201AndUpsertCustomer()
{
    // POST with firstName, lastName, mobile
    // Assert 201 response
    // Assert response contains firstName, lastName, mobile
    // Assert customer record was created in DB
}
```

**Step 3: Update existing functional tests**

Any tests that check customer registration/login endpoints need updated request payloads (firstName/lastName instead of name).

**Step 4: Run functional tests**

```bash
dotnet test tests/Chronith.Tests.Functional
```

**Step 5: Commit**

```bash
git add -A && git commit -m "test: update functional tests and seed data for contact fields refactor"
```

---

### Task 12: Update Dashboard (if applicable)

**Files:**

- Check: `dashboard/` directory for customer name/phone references

**Step 1: Search dashboard for customer references**

Search for `customer.name`, `customer.phone`, `Name`, `Phone` in dashboard components that display customer data.

**Step 2: Update to FirstName/LastName/Mobile**

Update any TypeScript types, API response handling, and UI display.

**Step 3: Commit**

```bash
git add -A && git commit -m "feat(dashboard): update customer display for FirstName/LastName/Mobile"
```

---

### Task 13: Final Verification

**Step 1: Build entire solution**

```bash
dotnet build Chronith.slnx
```

**Step 2: Run all tests**

```bash
dotnet test Chronith.slnx
```

**Step 3: Verify no secrets committed**

Check all changed files for real secrets or credentials.

**Step 4: Final commit if needed**

```bash
git add -A && git commit -m "chore: final cleanup for booking contact fields feature"
```
