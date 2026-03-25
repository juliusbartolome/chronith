# Custom Payment Redirect URLs Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Allow tenants to configure custom success/failure redirect URLs for payment checkout, with per-request overrides.

**Architecture:** Add `PaymentSuccessUrl`/`PaymentFailureUrl` to the `TenantPaymentConfig` domain model and propagate through all layers. The `CreatePublicCheckoutHandler` resolves the URL using a 3-tier priority: per-request > tenant config > global fallback. HMAC query params are always appended.

**Tech Stack:** .NET 10, FastEndpoints, MediatR, FluentValidation, EF Core + Npgsql, xUnit + FluentAssertions + NSubstitute

---

### Task 1: Domain Model — Add URL Fields to TenantPaymentConfig

**Files:**

- Modify: `src/Chronith.Domain/Models/TenantPaymentConfig.cs`
- Test: `tests/Chronith.Tests.Unit/Domain/TenantPaymentConfigTests.cs`

**Step 1: Write failing tests**

Add two tests to `TenantPaymentConfigTests.cs`:

```csharp
[Fact]
public void Create_WithCustomRedirectUrls_StoresUrls()
{
    var config = TenantPaymentConfig.Create(
        Guid.NewGuid(), "PayMongo", "Label", "{}", null, null,
        "https://mysite.com/success", "https://mysite.com/failed");

    config.PaymentSuccessUrl.Should().Be("https://mysite.com/success");
    config.PaymentFailureUrl.Should().Be("https://mysite.com/failed");
}

[Fact]
public void UpdateDetails_WithCustomRedirectUrls_UpdatesUrls()
{
    var config = TenantPaymentConfig.Create(
        Guid.NewGuid(), "PayMongo", "Label", "{}", null, null, null, null);
    config.PaymentSuccessUrl.Should().BeNull();

    config.UpdateDetails("Label", "{}", null, null,
        "https://mysite.com/success", "https://mysite.com/failed");

    config.PaymentSuccessUrl.Should().Be("https://mysite.com/success");
    config.PaymentFailureUrl.Should().Be("https://mysite.com/failed");
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Chronith.Tests.Unit --filter "TenantPaymentConfigTests" --no-restore -v minimal`
Expected: FAIL — `Create` method doesn't accept URL params, `PaymentSuccessUrl`/`PaymentFailureUrl` don't exist.

**Step 3: Implement domain model changes**

In `src/Chronith.Domain/Models/TenantPaymentConfig.cs`:

1. Add properties after `QrCodeUrl` (line 13):

```csharp
public string? PaymentSuccessUrl { get; private set; }
public string? PaymentFailureUrl { get; private set; }
```

2. Update `Create()` factory — add two optional params at end (lines 19-25):

```csharp
public static TenantPaymentConfig Create(
    Guid tenantId,
    string providerName,
    string label,
    string settings,
    string? publicNote,
    string? qrCodeUrl,
    string? paymentSuccessUrl = null,
    string? paymentFailureUrl = null)
    => new()
    {
        // ... existing fields ...
        PaymentSuccessUrl = paymentSuccessUrl,
        PaymentFailureUrl = paymentFailureUrl,
        // ...
    };
```

3. Update `UpdateDetails()` method — add two optional params (line 41):

```csharp
public void UpdateDetails(
    string label, string settings, string? publicNote, string? qrCodeUrl,
    string? paymentSuccessUrl = null, string? paymentFailureUrl = null)
{
    Label = label;
    Settings = settings;
    PublicNote = publicNote;
    QrCodeUrl = qrCodeUrl;
    PaymentSuccessUrl = paymentSuccessUrl;
    PaymentFailureUrl = paymentFailureUrl;
    UpdatedAt = DateTimeOffset.UtcNow;
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Chronith.Tests.Unit --filter "TenantPaymentConfigTests" --no-restore -v minimal`
Expected: ALL PASS (existing tests unaffected because new params are optional with default `null`).

**Step 5: Commit**

```
feat(domain): add PaymentSuccessUrl/PaymentFailureUrl to TenantPaymentConfig
```

---

### Task 2: Infrastructure — Entity, Configuration, Mapper

**Files:**

- Modify: `src/Chronith.Infrastructure/Persistence/Entities/TenantPaymentConfigEntity.cs`
- Modify: `src/Chronith.Infrastructure/Persistence/Configurations/TenantPaymentConfigConfiguration.cs`
- Modify: `src/Chronith.Infrastructure/Persistence/Mappers/TenantPaymentConfigEntityMapper.cs`

**Step 1: Update entity POCO**

In `TenantPaymentConfigEntity.cs`, add after `QrCodeUrl` (line 13):

```csharp
public string? PaymentSuccessUrl { get; set; }
public string? PaymentFailureUrl { get; set; }
```

**Step 2: Update EF configuration**

In `TenantPaymentConfigConfiguration.cs`, add after `QrCodeUrl` config (line 18):

```csharp
builder.Property(c => c.PaymentSuccessUrl).HasMaxLength(2048);
builder.Property(c => c.PaymentFailureUrl).HasMaxLength(2048);
```

**Step 3: Update entity mapper**

In `TenantPaymentConfigEntityMapper.cs`:

`ToDomain()` — add after `QrCodeUrl` line (line 19):

```csharp
SetProperty(domain, nameof(TenantPaymentConfig.PaymentSuccessUrl), entity.PaymentSuccessUrl);
SetProperty(domain, nameof(TenantPaymentConfig.PaymentFailureUrl), entity.PaymentFailureUrl);
```

`ToEntity()` — add after `QrCodeUrl` line (line 35):

```csharp
PaymentSuccessUrl = domain.PaymentSuccessUrl,
PaymentFailureUrl = domain.PaymentFailureUrl,
```

**Step 4: Build to verify compilation**

Run: `dotnet build src/Chronith.Infrastructure --no-restore -v minimal`
Expected: 0 errors.

**Step 5: Commit**

```
feat(infra): add payment redirect URL columns to TenantPaymentConfig entity
```

---

### Task 3: Application Layer — DTO, Mapper, Commands

**Files:**

- Modify: `src/Chronith.Application/DTOs/TenantPaymentConfigDto.cs`
- Modify: `src/Chronith.Application/Mappers/TenantPaymentConfigMapper.cs`
- Modify: `src/Chronith.Application/Commands/TenantPaymentConfig/CreateTenantPaymentConfigCommand.cs`
- Modify: `src/Chronith.Application/Commands/TenantPaymentConfig/UpdateTenantPaymentConfigCommand.cs`
- Test: `tests/Chronith.Tests.Unit/Application/CreateTenantPaymentConfigCommandHandlerTests.cs`
- Test: `tests/Chronith.Tests.Unit/Application/UpdateTenantPaymentConfigCommandHandlerTests.cs`

**Step 1: Write failing tests**

In `CreateTenantPaymentConfigCommandHandlerTests.cs`, add:

```csharp
[Fact]
public async Task Handle_WithRedirectUrls_CreatesConfigWithUrls()
{
    var handler = new CreateTenantPaymentConfigCommandHandler(_repo, _tenantContext, _unitOfWork);
    var cmd = new CreateTenantPaymentConfigCommand
    {
        ProviderName = "PayMongo",
        Label = "PayMongo Dev",
        Settings = """{"SecretKey":"sk_test"}""",
        PublicNote = null,
        QrCodeUrl = null,
        PaymentSuccessUrl = "https://myapp.com/success",
        PaymentFailureUrl = "https://myapp.com/failed"
    };

    var result = await handler.Handle(cmd, CancellationToken.None);

    result.PaymentSuccessUrl.Should().Be("https://myapp.com/success");
    result.PaymentFailureUrl.Should().Be("https://myapp.com/failed");
}
```

In `UpdateTenantPaymentConfigCommandHandlerTests.cs`, add:

```csharp
[Fact]
public async Task Handle_WithRedirectUrls_UpdatesUrls()
{
    var id = Guid.NewGuid();
    var existing = TenantPaymentConfig.Create(Guid.NewGuid(), "PayMongo", "Label", "{}", null, null);
    _repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(existing);

    var handler = new UpdateTenantPaymentConfigCommandHandler(_repo, _unitOfWork);
    var cmd = new UpdateTenantPaymentConfigCommand
    {
        Id = id,
        Label = "Label",
        Settings = "{}",
        PublicNote = null,
        QrCodeUrl = null,
        PaymentSuccessUrl = "https://myapp.com/success",
        PaymentFailureUrl = "https://myapp.com/failed"
    };

    var result = await handler.Handle(cmd, CancellationToken.None);

    result.PaymentSuccessUrl.Should().Be("https://myapp.com/success");
    result.PaymentFailureUrl.Should().Be("https://myapp.com/failed");
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Chronith.Tests.Unit --filter "CreateTenantPaymentConfigCommandHandlerTests|UpdateTenantPaymentConfigCommandHandlerTests" --no-restore -v minimal`
Expected: FAIL — `PaymentSuccessUrl`/`PaymentFailureUrl` don't exist on command or DTO.

**Step 3: Implement changes**

**DTO** (`TenantPaymentConfigDto.cs`) — add two fields at end:

```csharp
public sealed record TenantPaymentConfigDto(
    Guid Id, Guid TenantId, string ProviderName, string Label, bool IsActive,
    string? PublicNote, string? QrCodeUrl,
    string? PaymentSuccessUrl, string? PaymentFailureUrl,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
```

**Mapper** (`TenantPaymentConfigMapper.cs`) — update `ToDto()`:

```csharp
public static TenantPaymentConfigDto ToDto(this TenantPaymentConfig config) => new(
    config.Id, config.TenantId, config.ProviderName, config.Label, config.IsActive,
    config.PublicNote, config.QrCodeUrl,
    config.PaymentSuccessUrl, config.PaymentFailureUrl,
    config.CreatedAt, config.UpdatedAt);
```

**CreateCommand** — add optional properties:

```csharp
public string? PaymentSuccessUrl { get; init; }
public string? PaymentFailureUrl { get; init; }
```

**CreateCommand Validator** — add URI validation:

```csharp
RuleFor(x => x.PaymentSuccessUrl)
    .MaximumLength(2048)
    .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http"))
    .When(x => x.PaymentSuccessUrl is not null)
    .WithMessage("PaymentSuccessUrl must be a valid absolute URL");
RuleFor(x => x.PaymentFailureUrl)
    .MaximumLength(2048)
    .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http"))
    .When(x => x.PaymentFailureUrl is not null)
    .WithMessage("PaymentFailureUrl must be a valid absolute URL");
```

**CreateCommand Handler** — pass new params to `Create()`:

```csharp
var config = Domain.Models.TenantPaymentConfig.Create(
    tenantContext.TenantId,
    cmd.ProviderName, cmd.Label, cmd.Settings,
    cmd.PublicNote, cmd.QrCodeUrl,
    cmd.PaymentSuccessUrl, cmd.PaymentFailureUrl);
```

**UpdateCommand** — add optional properties:

```csharp
public string? PaymentSuccessUrl { get; init; }
public string? PaymentFailureUrl { get; init; }
```

**UpdateCommand Validator** — add same URI validation rules.

**UpdateCommand Handler** — pass new params to `UpdateDetails()`:

```csharp
config.UpdateDetails(cmd.Label, cmd.Settings, cmd.PublicNote, cmd.QrCodeUrl,
    cmd.PaymentSuccessUrl, cmd.PaymentFailureUrl);
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Chronith.Tests.Unit --filter "TenantPaymentConfig" --no-restore -v minimal`
Expected: ALL PASS.

**Step 5: Commit**

```
feat(app): add payment redirect URLs to TenantPaymentConfig commands and DTOs
```

---

### Task 4: API Layer — Endpoint Request Models

**Files:**

- Modify: `src/Chronith.API/Endpoints/Tenant/TenantPaymentConfigEndpoints.cs`
- Modify: `src/Chronith.API/Endpoints/Public/PublicCreateCheckoutEndpoint.cs`

**Step 1: Update tenant config request models**

In `TenantPaymentConfigEndpoints.cs`:

`CreateTenantPaymentConfigRequest` (line 13) — add:

```csharp
public string? PaymentSuccessUrl { get; set; }
public string? PaymentFailureUrl { get; set; }
```

`UpdateTenantPaymentConfigRequest` (line 22) — add:

```csharp
public string? PaymentSuccessUrl { get; set; }
public string? PaymentFailureUrl { get; set; }
```

Update `CreateTenantPaymentConfigEndpoint.HandleAsync` (line 67) to pass new fields:

```csharp
PaymentSuccessUrl = req.PaymentSuccessUrl,
PaymentFailureUrl = req.PaymentFailureUrl
```

Update `UpdateTenantPaymentConfigEndpoint.HandleAsync` (line 97) to pass new fields:

```csharp
PaymentSuccessUrl = req.PaymentSuccessUrl,
PaymentFailureUrl = req.PaymentFailureUrl
```

**Step 2: Update checkout request model**

In `PublicCreateCheckoutEndpoint.cs`:

`PublicCreateCheckoutRequest` (line 10) — add body fields:

```csharp
public string? SuccessUrl { get; set; }
public string? FailureUrl { get; set; }
```

Update `HandleAsync` (line 41) to pass new fields:

```csharp
var result = await sender.Send(new CreatePublicCheckoutCommand
{
    TenantSlug = req.TenantSlug,
    BookingId = req.BookingId,
    ProviderName = req.ProviderName,
    SuccessUrl = req.SuccessUrl,
    FailureUrl = req.FailureUrl
}, ct);
```

**Step 3: Build to verify compilation**

Run: `dotnet build src/Chronith.API --no-restore -v minimal`
Expected: 0 errors (command changes from Task 3 already accept these fields).

**Step 4: Commit**

```
feat(api): accept payment redirect URLs on config and checkout endpoints
```

---

### Task 5: Checkout Handler — URL Resolution Logic

**Files:**

- Modify: `src/Chronith.Application/Commands/Public/CreatePublicCheckoutCommand.cs`
- Test: `tests/Chronith.Tests.Unit/Application/CreatePublicCheckoutCommandHandlerTests.cs`

**Step 1: Write failing tests**

Add to `CreatePublicCheckoutCommandHandlerTests.cs`. The `Build()` helper needs updating to accept a `TenantPaymentConfig` and inject `ITenantPaymentConfigRepository`:

Update `Build()` to accept config and return new fields:

```csharp
private static (CreatePublicCheckoutHandler Handler, IPaymentProvider Provider, IBookingRepository BookingRepo)
    Build(Booking? booking = null, IPaymentProvider? provider = null,
          TenantPaymentConfig? tenantPaymentConfig = null)
{
    // ... existing setup ...

    var configRepo = Substitute.For<ITenantPaymentConfigRepository>();
    if (tenantPaymentConfig is not null)
    {
        configRepo.GetActiveByProviderNameAsync(TestTenant.Id, "PayMongo", Arg.Any<CancellationToken>())
            .Returns(tenantPaymentConfig);
    }

    var handler = new CreatePublicCheckoutHandler(
        bookingRepo, tenantRepo, resolver, signer, pageOptions, configRepo);

    return (handler, mockProvider, bookingRepo);
}
```

Add three new tests:

```csharp
[Fact]
public async Task Handle_WithRequestOverrideUrls_UsesRequestUrls()
{
    var (handler, provider, _) = Build();

    await handler.Handle(new CreatePublicCheckoutCommand
    {
        TenantSlug = "test-tenant",
        BookingId = BookingId,
        ProviderName = "PayMongo",
        SuccessUrl = "https://custom.com/success",
        FailureUrl = "https://custom.com/failed"
    }, CancellationToken.None);

    await provider.Received(1).CreateCheckoutSessionAsync(
        Arg.Is<CreateCheckoutRequest>(r =>
            r.SuccessUrl != null && r.SuccessUrl.Contains("custom.com/success")),
        Arg.Any<CancellationToken>());
}

[Fact]
public async Task Handle_WithTenantConfigUrls_UsesConfigUrls()
{
    var config = TenantPaymentConfig.Create(
        TestTenant.Id, "PayMongo", "Label", "{}", null, null,
        "https://tenant.com/success", "https://tenant.com/failed");
    var (handler, provider, _) = Build(tenantPaymentConfig: config);

    await handler.Handle(new CreatePublicCheckoutCommand
    {
        TenantSlug = "test-tenant",
        BookingId = BookingId,
        ProviderName = "PayMongo"
    }, CancellationToken.None);

    await provider.Received(1).CreateCheckoutSessionAsync(
        Arg.Is<CreateCheckoutRequest>(r =>
            r.SuccessUrl != null && r.SuccessUrl.Contains("tenant.com/success")),
        Arg.Any<CancellationToken>());
}

[Fact]
public async Task Handle_RequestOverrideTakesPriorityOverTenantConfig()
{
    var config = TenantPaymentConfig.Create(
        TestTenant.Id, "PayMongo", "Label", "{}", null, null,
        "https://tenant.com/success", "https://tenant.com/failed");
    var (handler, provider, _) = Build(tenantPaymentConfig: config);

    await handler.Handle(new CreatePublicCheckoutCommand
    {
        TenantSlug = "test-tenant",
        BookingId = BookingId,
        ProviderName = "PayMongo",
        SuccessUrl = "https://override.com/success",
        FailureUrl = "https://override.com/failed"
    }, CancellationToken.None);

    await provider.Received(1).CreateCheckoutSessionAsync(
        Arg.Is<CreateCheckoutRequest>(r =>
            r.SuccessUrl != null && r.SuccessUrl.Contains("override.com/success")),
        Arg.Any<CancellationToken>());
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Chronith.Tests.Unit --filter "CreatePublicCheckoutCommandHandlerTests" --no-restore -v minimal`
Expected: FAIL — command has no `SuccessUrl`/`FailureUrl`, handler constructor doesn't accept config repo.

**Step 3: Implement changes**

**Command record** — add optional fields:

```csharp
public sealed record CreatePublicCheckoutCommand : IRequest<CreateCheckoutResult>
{
    public required string TenantSlug { get; init; }
    public required Guid BookingId { get; init; }
    public required string ProviderName { get; init; }
    public string? SuccessUrl { get; init; }
    public string? FailureUrl { get; init; }
}
```

**Validator** — add URL validation rules:

```csharp
RuleFor(x => x.SuccessUrl)
    .MaximumLength(2048)
    .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http"))
    .When(x => x.SuccessUrl is not null)
    .WithMessage("SuccessUrl must be a valid absolute URL");
RuleFor(x => x.FailureUrl)
    .MaximumLength(2048)
    .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http"))
    .When(x => x.FailureUrl is not null)
    .WithMessage("FailureUrl must be a valid absolute URL");
```

**Handler** — inject `ITenantPaymentConfigRepository`, implement 3-tier resolution:

```csharp
public sealed class CreatePublicCheckoutHandler(
    IBookingRepository bookingRepo,
    ITenantRepository tenantRepo,
    ITenantPaymentProviderResolver resolver,
    IBookingUrlSigner signer,
    IOptions<PaymentPageOptions> pageOptions,
    ITenantPaymentConfigRepository configRepo)
    : IRequestHandler<CreatePublicCheckoutCommand, CreateCheckoutResult>
{
    public async Task<CreateCheckoutResult> Handle(
        CreatePublicCheckoutCommand cmd, CancellationToken ct)
    {
        var tenant = await tenantRepo.GetBySlugAsync(cmd.TenantSlug, ct)
            ?? throw new NotFoundException("Tenant", cmd.TenantSlug);

        var booking = await bookingRepo.GetPublicByIdAsync(tenant.Id, cmd.BookingId, ct)
            ?? throw new NotFoundException("Booking", cmd.BookingId);

        if (booking.Status != BookingStatus.PendingPayment)
            throw new InvalidStateTransitionException(booking.Status, "create checkout");

        var provider = await resolver.ResolveAsync(tenant.Id, cmd.ProviderName, ct)
            ?? throw new NotFoundException("PaymentProvider", cmd.ProviderName);

        // --- 3-tier URL resolution ---
        var baseUrl = pageOptions.Value.BaseUrl;
        var defaultSuccessUrl = $"{baseUrl}/success";
        var defaultFailureUrl = $"{baseUrl}/failed";

        // Check tenant payment config for custom URLs
        var config = await configRepo.GetActiveByProviderNameAsync(tenant.Id, cmd.ProviderName, ct);
        var configSuccessUrl = config?.PaymentSuccessUrl;
        var configFailureUrl = config?.PaymentFailureUrl;

        // Resolution: request override > tenant config > global fallback
        var resolvedSuccessBase = cmd.SuccessUrl ?? configSuccessUrl ?? defaultSuccessUrl;
        var resolvedFailureBase = cmd.FailureUrl ?? configFailureUrl ?? defaultFailureUrl;

        // Always append HMAC signature
        var successUrl = signer.GenerateSignedUrl(resolvedSuccessBase, cmd.BookingId, cmd.TenantSlug);
        var failureUrl = signer.GenerateSignedUrl(resolvedFailureBase, cmd.BookingId, cmd.TenantSlug);

        var checkoutResult = await provider.CreateCheckoutSessionAsync(
            new CreateCheckoutRequest(
                AmountInCentavos: booking.AmountInCentavos,
                Currency: booking.Currency,
                Description: $"Booking {booking.Id}",
                BookingId: booking.Id,
                TenantId: tenant.Id,
                SuccessUrl: successUrl,
                CancelUrl: failureUrl),
            ct);

        booking.SetCheckoutDetails(checkoutResult.CheckoutUrl, checkoutResult.ProviderTransactionId);
        await bookingRepo.UpdateAsync(booking, ct);

        return checkoutResult;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Chronith.Tests.Unit --filter "CreatePublicCheckoutCommandHandlerTests" --no-restore -v minimal`
Expected: ALL PASS.

**Step 5: Commit**

```
feat(app): implement 3-tier URL resolution in CreatePublicCheckoutHandler
```

---

### Task 6: EF Core Migration

**Files:**

- Create: `src/Chronith.Infrastructure/Migrations/PostgreSQL/<timestamp>_AddPaymentRedirectUrls.cs` (auto-generated)

**Step 1: Generate migration**

```bash
dotnet ef migrations add AddPaymentRedirectUrls \
  --project src/Chronith.Infrastructure \
  --startup-project src/Chronith.API \
  --output-dir Migrations/PostgreSQL
```

**Step 2: Review the generated migration**

Verify it adds two nullable `varchar(2048)` columns: `PaymentSuccessUrl` and `PaymentFailureUrl` to the `tenant_payment_configs` table. No data migration needed.

**Step 3: Build to verify**

Run: `dotnet build Chronith.slnx --no-restore -v minimal`
Expected: 0 errors.

**Step 4: Commit**

```
feat(infra): add EF migration for payment redirect URL columns
```

---

### Task 7: Full Build + All Tests

**Step 1: Build the entire solution**

Run: `dotnet build Chronith.slnx -v minimal`
Expected: 0 errors, 0 warnings.

**Step 2: Run unit tests**

Run: `dotnet test tests/Chronith.Tests.Unit --no-restore -v minimal`
Expected: ALL PASS.

**Step 3: Fix any compilation or test failures across the solution**

If any existing tests broke (e.g., due to DTO positional parameter reordering in `TenantPaymentConfigDto`), fix them. Common fixes:

- Tests constructing `TenantPaymentConfigDto` directly may need the two new fields added.
- Functional test seed data may need updating.

**Step 4: Commit fixes if any**

```
fix: update tests for TenantPaymentConfig DTO changes
```

---

### Task 8: Dashboard — Payment Config Form URL Fields (if form exists)

**Note:** No dashboard payment config UI currently exists. Skip this task unless one has been added by the time this plan executes.

If a form exists:

- Add two text input fields for PaymentSuccessUrl and PaymentFailureUrl
- Add placeholder text: "Leave empty to use default"
- Update the TypeScript types/interfaces for TenantPaymentConfig

---

### Task 9: Final Verification + PR

**Step 1: Full build**

Run: `dotnet build Chronith.slnx -v minimal`

**Step 2: Run all test suites**

Run: `dotnet test Chronith.slnx --no-restore -v minimal`

**Step 3: Push branch and create PR**

```bash
git push -u origin feat/custom-payment-redirects
gh pr create --title "feat: custom payment redirect URLs for tenant checkout" --body "$(cat <<'EOF'
## Summary
- Add `PaymentSuccessUrl` and `PaymentFailureUrl` fields to `TenantPaymentConfig` domain model, entity, DTO, and CRUD commands
- Implement 3-tier URL resolution in `CreatePublicCheckoutHandler`: per-request override > tenant config > global fallback
- HMAC query params always appended to resolved URL
- Accept optional `SuccessUrl`/`FailureUrl` on public checkout endpoint request
- EF Core migration adds two nullable columns

## Testing
- Unit tests for domain model, command handlers, and checkout handler URL resolution
- All existing tests pass unchanged (new params are optional with null defaults)
EOF
)"
```
