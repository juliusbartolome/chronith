# API Key Scope-Based RBAC Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the free-form `Role` string on API keys with a set of explicit, fine-grained scopes (e.g. `bookings:read`, `staff:write`) stored as a PostgreSQL `text[]` column, enforced via a custom ASP.NET Core authorization policy.

**Architecture:** `ApiKeyScope` static constants live in Domain. The auth handler emits one `scope` claim per granted scope plus a synthetic `"ApiKey"` role claim. Endpoints that accept API key callers add `AuthSchemes("Bearer", "ApiKey")`, include `"ApiKey"` in their `Roles(...)` call, and declare a scope policy via `Policies("scope:<name>")`. A custom `ApiKeyScopeHandler` evaluates the scope requirement only for API key callers — JWT callers always pass it.

**Tech Stack:** FastEndpoints 8.x, ASP.NET Core Authorization, Npgsql `text[]` column, xUnit + FluentAssertions + NSubstitute, Testcontainers (integration), MVC.Testing (functional).

**Design doc:** `docs/plans/2026-03-21-api-key-scopes-design.md`

---

## Pre-work: Create Worktree

```bash
# From repo root (main branch)
git worktree add .worktrees/api-key-scopes -b feat/api-key-scopes
```

All subsequent work is done inside `.worktrees/api-key-scopes/`.

---

## Task 1: Domain — `ApiKeyScope` constants

**Files:**

- Create: `src/Chronith.Domain/Models/ApiKeyScope.cs`
- Test: `tests/Chronith.Tests.Unit/Domain/ApiKeyScopeTests.cs`

**Step 1: Write the failing test**

```csharp
// tests/Chronith.Tests.Unit/Domain/ApiKeyScopeTests.cs
using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class ApiKeyScopeTests
{
    [Fact]
    public void All_ContainsExactlyFifteenScopes()
    {
        ApiKeyScope.All.Should().HaveCount(15);
    }

    [Fact]
    public void All_ContainsNoNullOrEmpty()
    {
        ApiKeyScope.All.Should().AllSatisfy(s => s.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void All_ContainsNoDuplicates()
    {
        var distinct = ApiKeyScope.All.Distinct().ToList();
        distinct.Should().HaveCount(ApiKeyScope.All.Count);
    }

    [Theory]
    [InlineData(ApiKeyScope.BookingsRead)]
    [InlineData(ApiKeyScope.BookingsWrite)]
    [InlineData(ApiKeyScope.BookingsDelete)]
    [InlineData(ApiKeyScope.BookingsConfirm)]
    [InlineData(ApiKeyScope.BookingsCancel)]
    [InlineData(ApiKeyScope.BookingsPay)]
    [InlineData(ApiKeyScope.AvailabilityRead)]
    [InlineData(ApiKeyScope.StaffRead)]
    [InlineData(ApiKeyScope.StaffWrite)]
    [InlineData(ApiKeyScope.BookingTypesRead)]
    [InlineData(ApiKeyScope.BookingTypesWrite)]
    [InlineData(ApiKeyScope.AnalyticsRead)]
    [InlineData(ApiKeyScope.WebhooksWrite)]
    [InlineData(ApiKeyScope.TenantRead)]
    [InlineData(ApiKeyScope.TenantWrite)]
    public void EachConstant_IsInAll(string scope)
    {
        ApiKeyScope.All.Should().Contain(scope);
    }
}
```

**Step 2: Run test to verify it fails**

```bash
dotnet test tests/Chronith.Tests.Unit --filter "ApiKeyScopeTests" -v q
```

Expected: FAIL — `ApiKeyScope` does not exist.

**Step 3: Create `ApiKeyScope.cs`**

```csharp
// src/Chronith.Domain/Models/ApiKeyScope.cs
namespace Chronith.Domain.Models;

public static class ApiKeyScope
{
    public const string BookingsRead    = "bookings:read";
    public const string BookingsWrite   = "bookings:write";
    public const string BookingsDelete  = "bookings:delete";
    public const string BookingsConfirm = "bookings:confirm";
    public const string BookingsCancel  = "bookings:cancel";
    public const string BookingsPay     = "bookings:pay";
    public const string AvailabilityRead  = "availability:read";
    public const string StaffRead         = "staff:read";
    public const string StaffWrite        = "staff:write";
    public const string BookingTypesRead  = "booking-types:read";
    public const string BookingTypesWrite = "booking-types:write";
    public const string AnalyticsRead     = "analytics:read";
    public const string WebhooksWrite     = "webhooks:write";
    public const string TenantRead        = "tenant:read";
    public const string TenantWrite       = "tenant:write";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        BookingsRead, BookingsWrite, BookingsDelete, BookingsConfirm, BookingsCancel,
        BookingsPay, AvailabilityRead, StaffRead, StaffWrite, BookingTypesRead,
        BookingTypesWrite, AnalyticsRead, WebhooksWrite, TenantRead, TenantWrite,
    };
}
```

**Step 4: Run test to verify it passes**

```bash
dotnet test tests/Chronith.Tests.Unit --filter "ApiKeyScopeTests" -v q
```

Expected: PASS (all 19 facts).

**Step 5: Commit**

```bash
git add src/Chronith.Domain/Models/ApiKeyScope.cs \
        tests/Chronith.Tests.Unit/Domain/ApiKeyScopeTests.cs
git commit -m "feat(domain): add ApiKeyScope constants"
```

---

## Task 2: Domain — Update `TenantApiKey` model

**Files:**

- Modify: `src/Chronith.Domain/Models/TenantApiKey.cs`
- Modify: `tests/Chronith.Tests.Unit/Domain/TenantApiKeyTests.cs`

**Step 1: Write the failing tests**

Add these tests to `TenantApiKeyTests.cs` (the existing tests will still compile once you update the model):

```csharp
[Fact]
public void Scopes_DefaultsToEmpty()
{
    var key = new TenantApiKey();
    key.Scopes.Should().BeEmpty();
}

[Fact]
public void Scopes_AreSameAsPassedIntoConstructor()
{
    var scopes = new[] { ApiKeyScope.BookingsRead, ApiKeyScope.StaffRead };
    var key = new TenantApiKey { Scopes = scopes };
    key.Scopes.Should().BeEquivalentTo(scopes);
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Chronith.Tests.Unit --filter "TenantApiKeyTests" -v q
```

Expected: FAIL — `Scopes` property does not exist.

**Step 3: Update `TenantApiKey.cs`**

Replace the `Role` property with `Scopes`. The model is currently a simple POCO with `init` setters (no factory method or validation at domain level — that stays in the Application layer).

```csharp
// src/Chronith.Domain/Models/TenantApiKey.cs
namespace Chronith.Domain.Models;

public sealed class TenantApiKey
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public string KeyHash { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> Scopes { get; init; } = [];
    public bool IsRevoked { get; private set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Returns true when this key has a set expiry date in the past.</summary>
    public bool IsExpired(DateTimeOffset now) =>
        ExpiresAt.HasValue && ExpiresAt.Value < now;

    public void Revoke() => IsRevoked = true;

    public void UpdateLastUsed(DateTimeOffset now)
    {
        if (LastUsedAt is null || now > LastUsedAt)
            LastUsedAt = now;
    }

    /// <summary>
    /// Generates a new raw API key and returns both the key and its hash.
    /// The raw key must be shown to the user once and never stored.
    /// </summary>
    public static (string RawKey, string KeyHash) GenerateKey()
    {
        var randomBytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(randomBytes);
        var rawKey = $"cth_{Convert.ToBase64String(randomBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=')}";
        var hash = ComputeHash(rawKey);
        return (rawKey, hash);
    }

    public static string ComputeHash(string rawKey)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(rawKey);
        var hashBytes = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hashBytes);
    }
}
```

**Step 4: Run tests**

```bash
dotnet test tests/Chronith.Tests.Unit --filter "TenantApiKeyTests" -v q
```

Expected: PASS (all tests including the new ones).

**Step 5: Commit**

```bash
git add src/Chronith.Domain/Models/TenantApiKey.cs \
        tests/Chronith.Tests.Unit/Domain/TenantApiKeyTests.cs
git commit -m "feat(domain): replace Role with Scopes on TenantApiKey"
```

---

## Task 3: Infrastructure — Entity, EF config, and mapper

**Files:**

- Modify: `src/Chronith.Infrastructure/Persistence/Entities/TenantApiKeyEntity.cs`
- Modify: `src/Chronith.Infrastructure/Persistence/Configurations/TenantApiKeyConfiguration.cs`
- Modify (or create): `src/Chronith.Infrastructure/Persistence/Mappers/TenantApiKeyEntityMapper.cs`

> **Note:** Check if `TenantApiKeyEntityMapper.cs` exists. If it does not, create it. If it does, update it.

**Step 1: Check if mapper exists**

```bash
ls src/Chronith.Infrastructure/Persistence/Mappers/TenantApiKey*
```

**Step 2: Update the entity**

```csharp
// src/Chronith.Infrastructure/Persistence/Entities/TenantApiKeyEntity.cs
namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class TenantApiKeyEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string KeyHash { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = [];
    public bool IsRevoked { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
```

**Step 3: Update the EF configuration**

```csharp
// src/Chronith.Infrastructure/Persistence/Configurations/TenantApiKeyConfiguration.cs
using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class TenantApiKeyConfiguration : IEntityTypeConfiguration<TenantApiKeyEntity>
{
    public void Configure(EntityTypeBuilder<TenantApiKeyEntity> builder)
    {
        builder.ToTable("tenant_api_keys", "chronith");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.KeyHash).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Scopes)
            .HasColumnType("text[]")
            .HasColumnName("scopes")
            .IsRequired();
        builder.Property(e => e.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired(false);

        // Primary auth lookup — unique hash
        builder.HasIndex(e => e.KeyHash)
            .IsUnique()
            .HasDatabaseName("IX_tenant_api_keys_key_hash");

        // List endpoint filter
        builder.HasIndex(e => new { e.TenantId, e.IsRevoked })
            .HasDatabaseName("IX_tenant_api_keys_tenant_id_is_revoked");
    }
}
```

**Step 4: Create or update the mapper**

If `TenantApiKeyEntityMapper.cs` does not exist, check how the repository maps entity → domain (it may do inline mapping). Find it:

```bash
grep -r "TenantApiKeyEntity\|ApiKeyRepo\|TenantApiKey" \
  src/Chronith.Infrastructure/Persistence/Repositories/ --include="*.cs" -l
```

Then find the mapping code and update it to map `Scopes` instead of `Role`. If there is a dedicated mapper file, update it:

```csharp
// src/Chronith.Infrastructure/Persistence/Mappers/TenantApiKeyEntityMapper.cs
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

public static class TenantApiKeyEntityMapper
{
    public static TenantApiKey ToDomain(this TenantApiKeyEntity e) =>
        new()
        {
            Id = e.Id,
            TenantId = e.TenantId,
            KeyHash = e.KeyHash,
            Description = e.Description,
            Scopes = e.Scopes.AsReadOnly(),
            IsRevoked = e.IsRevoked,
            CreatedAt = e.CreatedAt,
            LastUsedAt = e.LastUsedAt,
            ExpiresAt = e.ExpiresAt,
        };

    public static TenantApiKeyEntity ToEntity(this TenantApiKey d) =>
        new()
        {
            Id = d.Id,
            TenantId = d.TenantId,
            KeyHash = d.KeyHash,
            Description = d.Description,
            Scopes = [.. d.Scopes],
            IsRevoked = d.IsRevoked,
            CreatedAt = d.CreatedAt,
            LastUsedAt = d.LastUsedAt,
            ExpiresAt = d.ExpiresAt,
        };
}
```

If the repository does inline mapping (no dedicated mapper file), update the mapping inline — replace `Role = e.Role` with `Scopes = e.Scopes.AsReadOnly()` and `Role = d.Role` with `Scopes = [.. d.Scopes]`.

**Step 5: Build to verify no compile errors**

```bash
dotnet build Chronith.slnx -v q
```

Expected: Build succeeds (may have warnings from usages of the old `Role` property elsewhere — fix them all now).

**Step 6: Fix all remaining `Role` property usages**

```bash
grep -r "\.Role\b\|\"Role\"\|role\s*=" \
  src/ tests/ --include="*.cs" | grep -v "obj/"
```

At this point, remaining `Role` references will be in:

- `src/Chronith.Application/Commands/ApiKeys/CreateApiKeyCommand.cs` (Task 4)
- `src/Chronith.Application/DTOs/ApiKeyDto.cs` (Task 5)
- `src/Chronith.Application/DTOs/CreateApiKeyResult.cs` (Task 5)
- `src/Chronith.Application/Queries/ApiKeys/ListApiKeysQuery.cs` (Task 5)
- `src/Chronith.API/Endpoints/ApiKeys/CreateApiKeyEndpoint.cs` (Task 5)
- `src/Chronith.Infrastructure/Auth/ApiKeyAuthenticationHandler.cs` (Task 6)
- Test files (Tasks 4–6)

Leave these for their respective tasks. Build should still succeed once infrastructure changes are self-consistent.

**Step 7: Commit**

```bash
git add src/Chronith.Infrastructure/Persistence/Entities/TenantApiKeyEntity.cs \
        src/Chronith.Infrastructure/Persistence/Configurations/TenantApiKeyConfiguration.cs \
        src/Chronith.Infrastructure/Persistence/Mappers/TenantApiKeyEntityMapper.cs
git commit -m "feat(infra): update TenantApiKey entity and config for scopes text[]"
```

---

## Task 4: Infrastructure — EF Migration

**Step 1: Generate the migration**

```bash
dotnet ef migrations add AddApiKeyScopesDropRole \
  --project src/Chronith.Infrastructure \
  --startup-project src/Chronith.API \
  --output-dir Migrations/PostgreSQL
```

**Step 2: Review the generated migration**

Open the generated file. It should contain:

- `AddColumn` for `scopes text[]`
- `DropColumn` for `role`

If the generated SQL does not look right (e.g. `role` column is missing from the Up migration), manually adjust the migration's `Up()` and `Down()` methods.

**Step 3: Commit**

```bash
git add src/Chronith.Infrastructure/Migrations/
git commit -m "feat(infra): add migration AddApiKeyScopesDropRole"
```

---

## Task 5: Integration test — `text[]` round-trip

**Files:**

- Modify: `tests/Chronith.Tests.Integration/Persistence/TenantApiKeyRepositoryTests.cs`

**Step 1: Write the failing tests**

Update the existing integration test to remove the `Role` field and add `Scopes`. Also add a new round-trip test:

```csharp
[Fact]
public async Task Insert_AndQuery_ByKeyHash_WithScopes()
{
    var tenantId = Guid.NewGuid();
    await using var db = await DbContextFactory.CreateAsync(
        postgres.ConnectionString, tenantId, applyMigrations: true);

    var (_, keyHash) = Chronith.Domain.Models.TenantApiKey.GenerateKey();

    var entity = new TenantApiKeyEntity
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        KeyHash = keyHash,
        Description = "Test API key",
        Scopes = ["bookings:read", "staff:read"],
        IsRevoked = false,
        CreatedAt = DateTimeOffset.UtcNow
    };

    db.TenantApiKeys.Add(entity);
    await db.SaveChangesAsync();

    var found = await db.TenantApiKeys
        .FirstOrDefaultAsync(k => k.KeyHash == keyHash);

    found.Should().NotBeNull();
    found!.Scopes.Should().BeEquivalentTo(["bookings:read", "staff:read"]);
}

[Fact]
public async Task Insert_EmptyScopes_RoundTrips()
{
    var tenantId = Guid.NewGuid();
    await using var db = await DbContextFactory.CreateAsync(
        postgres.ConnectionString, tenantId, applyMigrations: true);

    var (_, keyHash) = Chronith.Domain.Models.TenantApiKey.GenerateKey();

    var entity = new TenantApiKeyEntity
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        KeyHash = keyHash,
        Description = "Empty scope key",
        Scopes = [],
        IsRevoked = false,
        CreatedAt = DateTimeOffset.UtcNow
    };

    db.TenantApiKeys.Add(entity);
    await db.SaveChangesAsync();

    var found = await db.TenantApiKeys
        .AsNoTracking()
        .FirstOrDefaultAsync(k => k.KeyHash == keyHash);

    found.Should().NotBeNull();
    found!.Scopes.Should().BeEmpty();
}
```

Also update the existing `Insert_AndQuery_ByKeyHash` test: replace `Role = "ReadOnly"` with `Scopes = ["bookings:read"]` and remove the `found.Role.Should()...` assertion. Replace it with `found.Scopes.Should().BeEquivalentTo(["bookings:read"])`.

**Step 2: Run to verify they fail**

```bash
dotnet test tests/Chronith.Tests.Integration --filter "TenantApiKeyRepositoryTests" -v q
```

Expected: compile error or test failure (old `Role` field still referenced or migration not applied).

**Step 3: Run to verify they pass**

After Tasks 3 and 4 are complete, run again:

```bash
dotnet test tests/Chronith.Tests.Integration --filter "TenantApiKeyRepositoryTests" -v q
```

Expected: PASS.

**Step 4: Commit**

```bash
git add tests/Chronith.Tests.Integration/Persistence/TenantApiKeyRepositoryTests.cs
git commit -m "test(integration): update TenantApiKeyRepositoryTests for scopes"
```

---

## Task 6: Application — DTOs and `ListApiKeysQuery`

**Files:**

- Modify: `src/Chronith.Application/DTOs/ApiKeyDto.cs`
- Modify: `src/Chronith.Application/DTOs/CreateApiKeyResult.cs`
- Modify: `src/Chronith.Application/Queries/ApiKeys/ListApiKeysQuery.cs`

**Step 1: Update `ApiKeyDto.cs`**

```csharp
// src/Chronith.Application/DTOs/ApiKeyDto.cs
namespace Chronith.Application.DTOs;

public sealed record ApiKeyDto(
    Guid Id,
    string Description,
    IReadOnlyList<string> Scopes,
    bool IsRevoked,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt);
```

**Step 2: Update `CreateApiKeyResult.cs`**

```csharp
// src/Chronith.Application/DTOs/CreateApiKeyResult.cs
namespace Chronith.Application.DTOs;

// RawKey is returned once and never stored; all other fields come from the stored record
public sealed record CreateApiKeyResult(
    Guid Id,
    string RawKey,
    string Description,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAt);
```

**Step 3: Update `ListApiKeysQuery.cs`**

Update the handler to map `Scopes` instead of `Role`:

```csharp
return keys
    .Select(k => new ApiKeyDto(
        Id: k.Id,
        Description: k.Description,
        Scopes: k.Scopes,
        IsRevoked: k.IsRevoked,
        CreatedAt: k.CreatedAt,
        LastUsedAt: k.LastUsedAt))
    .ToList();
```

**Step 4: Build**

```bash
dotnet build Chronith.slnx -v q
```

Fix any remaining compile errors from the DTO changes.

**Step 5: Commit**

```bash
git add src/Chronith.Application/DTOs/ApiKeyDto.cs \
        src/Chronith.Application/DTOs/CreateApiKeyResult.cs \
        src/Chronith.Application/Queries/ApiKeys/ListApiKeysQuery.cs
git commit -m "feat(app): update ApiKeyDto and CreateApiKeyResult to use Scopes"
```

---

## Task 7: Application — `CreateApiKeyCommand` refactor

**Files:**

- Modify: `src/Chronith.Application/Commands/ApiKeys/CreateApiKeyCommand.cs`
- Modify: `tests/Chronith.Tests.Unit/Application/CreateApiKeyHandlerTests.cs`

**Step 1: Write the failing unit tests**

Replace the entire `CreateApiKeyHandlerTests.cs`:

```csharp
using Chronith.Application.Commands.ApiKeys;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class CreateApiKeyHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static (CreateApiKeyHandler Handler, IApiKeyRepository ApiKeyRepo, IUnitOfWork UnitOfWork)
        Build()
    {
        var tenantCtx = Substitute.For<ITenantContext>();
        tenantCtx.TenantId.Returns(TenantId);

        var apiKeyRepo = Substitute.For<IApiKeyRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();

        var handler = new CreateApiKeyHandler(tenantCtx, apiKeyRepo, unitOfWork);
        return (handler, apiKeyRepo, unitOfWork);
    }

    [Fact]
    public async Task Handle_CreatesKeyWithCorrectTenantIdAndScopes()
    {
        // Arrange
        var (handler, apiKeyRepo, _) = Build();
        var cmd = new CreateApiKeyCommand
        {
            Description = "Test Key",
            Scopes = [ApiKeyScope.BookingsRead, ApiKeyScope.StaffRead],
        };

        TenantApiKey? captured = null;
        await apiKeyRepo.AddAsync(
            Arg.Do<TenantApiKey>(k => captured = k),
            Arg.Any<CancellationToken>());

        // Act
        var result = await handler.Handle(cmd, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(TenantId);
        captured.Description.Should().Be("Test Key");
        captured.Scopes.Should().BeEquivalentTo([ApiKeyScope.BookingsRead, ApiKeyScope.StaffRead]);
        result.RawKey.Should().StartWith("cth_");
        result.Scopes.Should().BeEquivalentTo([ApiKeyScope.BookingsRead, ApiKeyScope.StaffRead]);
    }

    [Fact]
    public async Task Handle_CallsSaveChanges()
    {
        var (handler, _, unitOfWork) = Build();
        var cmd = new CreateApiKeyCommand
        {
            Description = "Key",
            Scopes = [ApiKeyScope.BookingsRead],
        };

        await handler.Handle(cmd, CancellationToken.None);

        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

**Step 2: Write the failing validator tests**

Create `tests/Chronith.Tests.Unit/Application/CreateApiKeyValidatorTests.cs`:

```csharp
using Chronith.Application.Commands.ApiKeys;
using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Application;

public sealed class CreateApiKeyValidatorTests
{
    private readonly CreateApiKeyValidator _validator = new();

    [Fact]
    public async Task Validate_ValidScopes_IsValid()
    {
        var cmd = new CreateApiKeyCommand
        {
            Description = "My key",
            Scopes = [ApiKeyScope.BookingsRead, ApiKeyScope.StaffRead],
        };

        var result = await _validator.ValidateAsync(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyScopes_IsInvalid()
    {
        var cmd = new CreateApiKeyCommand
        {
            Description = "My key",
            Scopes = [],
        };

        var result = await _validator.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Scopes");
    }

    [Fact]
    public async Task Validate_UnknownScope_IsInvalid()
    {
        var cmd = new CreateApiKeyCommand
        {
            Description = "My key",
            Scopes = [ApiKeyScope.BookingsRead, "totally:invalid"],
        };

        var result = await _validator.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("totally:invalid"));
    }

    [Fact]
    public async Task Validate_EmptyDescription_IsInvalid()
    {
        var cmd = new CreateApiKeyCommand
        {
            Description = "",
            Scopes = [ApiKeyScope.BookingsRead],
        };

        var result = await _validator.ValidateAsync(cmd);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Description");
    }
}
```

**Step 3: Run to verify they fail**

```bash
dotnet test tests/Chronith.Tests.Unit \
  --filter "CreateApiKeyHandlerTests|CreateApiKeyValidatorTests" -v q
```

Expected: FAIL — `Scopes` property does not exist on command.

**Step 4: Update `CreateApiKeyCommand.cs`**

```csharp
// src/Chronith.Application/Commands/ApiKeys/CreateApiKeyCommand.cs
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.ApiKeys;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record CreateApiKeyCommand : IRequest<CreateApiKeyResult>, IAuditable
{
    public required string Description { get; init; }
    public required IEnumerable<string> Scopes { get; init; }

    // IAuditable — EntityId is Guid.Empty pre-creation
    public Guid EntityId => Guid.Empty;
    public string EntityType => "TenantApiKey";
    public string Action => "Create";
}

// ── Validator ────────────────────────────────────────────────────────────────

public sealed class CreateApiKeyValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Scopes)
            .NotEmpty()
            .WithMessage("At least one scope is required.")
            .ForEach(scope => scope
                .Must(s => ApiKeyScope.All.Contains(s))
                .WithMessage(s => $"'{s}' is not a valid API key scope."));
    }
}

// ── Handler ──────────────────────────────────────────────────────────────────

public sealed class CreateApiKeyHandler(
    ITenantContext tenantContext,
    IApiKeyRepository apiKeyRepo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateApiKeyCommand, CreateApiKeyResult>
{
    public async Task<CreateApiKeyResult> Handle(CreateApiKeyCommand cmd, CancellationToken ct)
    {
        var (rawKey, keyHash) = TenantApiKey.GenerateKey();

        var key = new TenantApiKey
        {
            TenantId = tenantContext.TenantId,
            KeyHash = keyHash,
            Description = cmd.Description,
            Scopes = cmd.Scopes.ToList().AsReadOnly(),
        };

        await apiKeyRepo.AddAsync(key, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new CreateApiKeyResult(
            Id: key.Id,
            RawKey: rawKey,
            Description: key.Description,
            Scopes: key.Scopes,
            CreatedAt: key.CreatedAt);
    }
}
```

**Step 5: Run tests to verify they pass**

```bash
dotnet test tests/Chronith.Tests.Unit \
  --filter "CreateApiKeyHandlerTests|CreateApiKeyValidatorTests" -v q
```

Expected: PASS (all tests).

**Step 6: Commit**

```bash
git add src/Chronith.Application/Commands/ApiKeys/CreateApiKeyCommand.cs \
        tests/Chronith.Tests.Unit/Application/CreateApiKeyHandlerTests.cs \
        tests/Chronith.Tests.Unit/Application/CreateApiKeyValidatorTests.cs
git commit -m "feat(app): replace Role with Scopes in CreateApiKeyCommand"
```

---

## Task 8: API — Update `CreateApiKeyEndpoint` request model

**Files:**

- Modify: `src/Chronith.API/Endpoints/ApiKeys/CreateApiKeyEndpoint.cs`

**Step 1: Update the request model and endpoint handler**

```csharp
// src/Chronith.API/Endpoints/ApiKeys/CreateApiKeyEndpoint.cs
using Chronith.Application.Commands.ApiKeys;
using Chronith.Application.DTOs;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.ApiKeys;

public sealed class CreateApiKeyRequest
{
    public string Description { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = [];
}

public sealed class CreateApiKeyEndpoint(ISender sender)
    : Endpoint<CreateApiKeyRequest, CreateApiKeyResult>
{
    public override void Configure()
    {
        Post("/tenant/api-keys");
        Roles("TenantAdmin");
        Options(x => x.WithTags("ApiKeys").RequireRateLimiting("Authenticated"));
        // Intentionally Bearer-only: API keys cannot create other API keys.
    }

    public override async Task HandleAsync(CreateApiKeyRequest req, CancellationToken ct)
    {
        var result = await sender.Send(new CreateApiKeyCommand
        {
            Description = req.Description,
            Scopes = req.Scopes,
        }, ct);

        await Send.ResponseAsync(result, 201, ct);
    }
}
```

**Step 2: Build**

```bash
dotnet build Chronith.slnx -v q
```

Expected: Build succeeds with no errors.

**Step 3: Commit**

```bash
git add src/Chronith.API/Endpoints/ApiKeys/CreateApiKeyEndpoint.cs
git commit -m "feat(api): update CreateApiKeyEndpoint for scopes"
```

---

## Task 9: Infrastructure — Auth handler scope claims

**Files:**

- Modify: `src/Chronith.Infrastructure/Auth/ApiKeyAuthenticationHandler.cs`
- Modify: `tests/Chronith.Tests.Unit/Infrastructure/ApiKeyAuthenticationHandlerTests.cs`

**Step 1: Write failing tests**

Update `ApiKeyAuthenticationHandlerTests.cs`. The existing `ValidKey_ReturnsSuccess_WithClaims` test currently asserts `ClaimTypes.Role == "Admin"`. Replace/expand that test:

```csharp
[Fact]
public async Task HandleAuthenticateAsync_ValidKey_EmitsScopeClaimsAndSyntheticRole()
{
    // Arrange
    var (rawKey, hash) = TenantApiKey.GenerateKey();
    var tenantId = Guid.NewGuid();
    var keyId = Guid.NewGuid();
    var apiKey = new TenantApiKey
    {
        Id = keyId,
        TenantId = tenantId,
        KeyHash = hash,
        Description = "test",
        Scopes = [ApiKeyScope.BookingsRead, ApiKeyScope.StaffRead],
    };

    var repo = Substitute.For<IApiKeyRepository>();
    repo.GetByHashAsync(hash, Arg.Any<CancellationToken>()).Returns(apiKey);
    repo.UpdateLastUsedAtAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
        .Returns(Task.CompletedTask);

    var handler = await BuildHandlerAsync(repo, headerValue: rawKey);

    // Act
    var result = await handler.AuthenticateAsync();
    await Task.Delay(50); // allow fire-and-forget to complete

    // Assert
    result.Succeeded.Should().BeTrue();
    var claims = result.Principal!.Claims.ToList();

    // Scope claims — one per scope
    claims.Should().Contain(c => c.Type == "scope" && c.Value == ApiKeyScope.BookingsRead);
    claims.Should().Contain(c => c.Type == "scope" && c.Value == ApiKeyScope.StaffRead);

    // Synthetic "ApiKey" role claim
    claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "ApiKey");

    // No old free-form role
    claims.Should().NotContain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");

    // Tenant and identity claims still present
    claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == tenantId.ToString());
    claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == keyId.ToString());
}

[Fact]
public async Task HandleAuthenticateAsync_KeyWithNoScopes_EmitsSyntheticRoleOnly()
{
    var (rawKey, hash) = TenantApiKey.GenerateKey();
    var apiKey = new TenantApiKey
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        KeyHash = hash,
        Description = "empty scopes",
        Scopes = [],
    };

    var repo = Substitute.For<IApiKeyRepository>();
    repo.GetByHashAsync(hash, Arg.Any<CancellationToken>()).Returns(apiKey);
    repo.UpdateLastUsedAtAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
        .Returns(Task.CompletedTask);

    var handler = await BuildHandlerAsync(repo, headerValue: rawKey);
    var result = await handler.AuthenticateAsync();

    result.Succeeded.Should().BeTrue();
    var claims = result.Principal!.Claims.ToList();
    claims.Should().NotContain(c => c.Type == "scope");
    claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "ApiKey");
}
```

Also add `using Chronith.Domain.Models;` to the test file if not already present.

**Step 2: Run to verify they fail**

```bash
dotnet test tests/Chronith.Tests.Unit --filter "ApiKeyAuthenticationHandlerTests" -v q
```

Expected: FAIL — claims assertions don't match current handler.

**Step 3: Update `ApiKeyAuthenticationHandler.cs`**

```csharp
// src/Chronith.Infrastructure/Auth/ApiKeyAuthenticationHandler.cs
using System.Security.Claims;
using System.Text.Encodings.Web;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Auth;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IApiKeyRepository apiKeyRepo,
    IServiceScopeFactory scopeFactory)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
{
    private const string HeaderName = "X-Api-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var rawKeyValues)
            || string.IsNullOrWhiteSpace(rawKeyValues))
            return AuthenticateResult.NoResult();

        var rawKey = rawKeyValues.ToString();
        var hash = TenantApiKey.ComputeHash(rawKey);
        var key = await apiKeyRepo.GetByHashAsync(hash, Context.RequestAborted);

        if (key is null)
            return AuthenticateResult.Fail("Invalid or revoked API key");

        if (key.IsExpired(DateTimeOffset.UtcNow))
            return AuthenticateResult.Fail("API key has expired");

        _ = UpdateLastUsedAtSafeAsync(scopeFactory, key.Id, Logger);

        // Emit one scope claim per granted scope
        var claims = new List<Claim>
        {
            new("tenant_id", key.TenantId.ToString()),
            new(ClaimTypes.NameIdentifier, key.Id.ToString()),
            new("sub", key.Id.ToString()),
            // Synthetic role so AllowRoles("ApiKey") can gate API-key-capable endpoints
            new(ClaimTypes.Role, "ApiKey"),
        };

        foreach (var scope in key.Scopes)
            claims.Add(new Claim("scope", scope));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    private static async Task UpdateLastUsedAtSafeAsync(
        IServiceScopeFactory scopeFactory, Guid id, ILogger logger)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();
            await repo.UpdateLastUsedAtAsync(id, DateTimeOffset.UtcNow, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to update LastUsedAt for key {Id}", id);
        }
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Chronith.Tests.Unit --filter "ApiKeyAuthenticationHandlerTests" -v q
```

Expected: PASS (all tests including old ones, since existing tests asserted `ClaimTypes.Role == "Admin"` which you've replaced with the new assertions).

**Step 5: Commit**

```bash
git add src/Chronith.Infrastructure/Auth/ApiKeyAuthenticationHandler.cs \
        tests/Chronith.Tests.Unit/Infrastructure/ApiKeyAuthenticationHandlerTests.cs
git commit -m "feat(infra): emit scope claims and synthetic ApiKey role in auth handler"
```

---

## Task 10: API — `ApiKeyScopeRequirement` and `ApiKeyScopeHandler`

**Files:**

- Create: `src/Chronith.API/Authorization/ApiKeyScopeRequirement.cs`
- Create: `src/Chronith.API/Authorization/ApiKeyScopeHandler.cs`

**Step 1: Create the requirement**

```csharp
// src/Chronith.API/Authorization/ApiKeyScopeRequirement.cs
using Microsoft.AspNetCore.Authorization;

namespace Chronith.API.Authorization;

/// <summary>
/// Authorization requirement: if the caller authenticated via the ApiKey scheme,
/// they must have the specified scope claim. JWT callers always satisfy this requirement.
/// </summary>
public sealed class ApiKeyScopeRequirement(string scope) : IAuthorizationRequirement
{
    public string Scope { get; } = scope;
}
```

**Step 2: Create the handler**

```csharp
// src/Chronith.API/Authorization/ApiKeyScopeHandler.cs
using Chronith.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;

namespace Chronith.API.Authorization;

public sealed class ApiKeyScopeHandler : AuthorizationHandler<ApiKeyScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ApiKeyScopeRequirement requirement)
    {
        // If the caller did NOT authenticate via the ApiKey scheme, bypass the scope check.
        // JWT callers use role-based auth — they always satisfy scope requirements.
        var isApiKeyCaller = context.User.Identities
            .Any(i => i.AuthenticationType == ApiKeyAuthenticationOptions.SchemeLabel);

        if (!isApiKeyCaller)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // API key caller: must possess the required scope claim
        if (context.User.HasClaim("scope", requirement.Scope))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
```

**Step 3: Register in `Program.cs`**

In `Program.cs`, find where `.AddAuthorization()` is called (around line 70) and replace it:

```csharp
// Replace:
.AddAuthorization()

// With:
.AddAuthorization(options =>
{
    // Register one named policy per scope for use with Policies("scope:xxx") on endpoints
    foreach (var scope in Chronith.Domain.Models.ApiKeyScope.All)
        options.AddPolicy($"scope:{scope}",
            p => p.AddRequirements(new ApiKeyScopeRequirement(scope)));
})
```

Also register the handler in DI. Find the `services.AddInfrastructure(...)` block and add after it (or in the infrastructure DI registration):

```csharp
builder.Services.AddSingleton<IAuthorizationHandler, ApiKeyScopeHandler>();
```

Add the necessary `using` statements at the top of `Program.cs`:

```csharp
using Chronith.API.Authorization;
using Microsoft.AspNetCore.Authorization;
```

**Step 4: Build**

```bash
dotnet build Chronith.slnx -v q
```

Expected: Build succeeds.

**Step 5: Commit**

```bash
git add src/Chronith.API/Authorization/ApiKeyScopeRequirement.cs \
        src/Chronith.API/Authorization/ApiKeyScopeHandler.cs \
        src/Chronith.API/Program.cs
git commit -m "feat(api): add ApiKeyScopeRequirement, ApiKeyScopeHandler, and scope policies"
```

---

## Task 11: API — Wire scopes on `ListApiKeysEndpoint` (representative example)

**Files:**

- Modify: `src/Chronith.API/Endpoints/ApiKeys/ListApiKeysEndpoint.cs`

`ListApiKeysEndpoint` already uses `AuthSchemes("Bearer", "ApiKey")`. After this task, API key callers must have `tenant:read` scope to list keys.

**Step 1: Update the endpoint**

```csharp
// src/Chronith.API/Endpoints/ApiKeys/ListApiKeysEndpoint.cs
using Chronith.Application.DTOs;
using Chronith.Application.Queries.ApiKeys;
using Chronith.Domain.Models;
using FastEndpoints;
using MediatR;

namespace Chronith.API.Endpoints.ApiKeys;

public sealed class ListApiKeysEndpoint(ISender sender)
    : EndpointWithoutRequest<IReadOnlyList<ApiKeyDto>>
{
    public override void Configure()
    {
        Get("/tenant/api-keys");
        Roles("TenantAdmin", "ApiKey");
        AuthSchemes("Bearer", "ApiKey");
        Policies($"scope:{ApiKeyScope.TenantRead}");
        Options(x => x.WithTags("ApiKeys").RequireRateLimiting("Authenticated"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await sender.Send(new ListApiKeysQuery(), ct);
        await Send.OkAsync(result, ct);
    }
}
```

**Key pattern to follow for all other endpoints:**

- Add `"ApiKey"` to `Roles(...)`
- Add `AuthSchemes("Bearer", "ApiKey")`
- Add `Policies($"scope:{ApiKeyScope.XxxYyy}")`

**Step 2: Build and run unit tests**

```bash
dotnet build Chronith.slnx -v q && dotnet test tests/Chronith.Tests.Unit -v q
```

Expected: Build and all unit tests pass.

**Step 3: Commit**

```bash
git add src/Chronith.API/Endpoints/ApiKeys/ListApiKeysEndpoint.cs
git commit -m "feat(api): wire scope:tenant:read on ListApiKeysEndpoint"
```

---

## Task 12: Functional tests — Update and add scope tests

**Files:**

- Modify: `tests/Chronith.Tests.Functional/ApiKeys/ApiKeyEndpointsTests.cs`

**Step 1: Update existing tests and add scope tests**

The existing tests use `role = "TenantAdmin"` — replace with `scopes = [...]`.

Key changes:

- `CreateApiKey_AsAdmin_Returns201WithRawKey` — send `scopes` instead of `role`; assert `result.Scopes` is not empty
- `CreateApiKey_AsStaff_Returns403` — send `scopes = ["bookings:read"]` (still 403 because TenantStaff can't create keys)
- `ListApiKeys_AsAdmin_ReturnsKeys` — update payload to `scopes`; assert DTO has `Scopes` not `Role`
- `AuthenticateWithApiKey_ValidKey_SucceedsOnProtectedEndpoint` — create key with `scopes = ["tenant:read"]`, use it on `GET /tenant/api-keys`

New tests to add:

```csharp
[Fact]
public async Task CreateApiKey_WithUnknownScope_Returns400()
{
    await EnsureSeedAsync();
    var client = fixture.CreateClient("TenantAdmin");

    var response = await client.PostAsJsonAsync(ApiKeysUrl, new
    {
        description = "Bad scope key",
        scopes = new[] { "totally:invalid" }
    });

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

[Fact]
public async Task CreateApiKey_WithEmptyScopes_Returns400()
{
    await EnsureSeedAsync();
    var client = fixture.CreateClient("TenantAdmin");

    var response = await client.PostAsJsonAsync(ApiKeysUrl, new
    {
        description = "No scopes key",
        scopes = Array.Empty<string>()
    });

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

[Fact]
public async Task ApiKeyWithoutRequiredScope_Returns403()
{
    await EnsureSeedAsync();
    var adminClient = fixture.CreateClient("TenantAdmin");

    // Create a key with bookings:read only (NOT tenant:read)
    var createResp = await adminClient.PostAsJsonAsync(ApiKeysUrl, new
    {
        description = $"Narrow scope key {Guid.NewGuid():N}",
        scopes = new[] { ApiKeyScope.BookingsRead }
    });
    createResp.StatusCode.Should().Be(HttpStatusCode.Created);
    var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

    // Try to list API keys — requires tenant:read scope
    var apiKeyClient = fixture.CreateAnonymousClient();
    apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

    var listResp = await apiKeyClient.GetAsync(ApiKeysUrl);
    listResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}

[Fact]
public async Task ApiKeyWithMatchingScope_Returns200()
{
    await EnsureSeedAsync();
    var adminClient = fixture.CreateClient("TenantAdmin");

    // Create a key with tenant:read
    var createResp = await adminClient.PostAsJsonAsync(ApiKeysUrl, new
    {
        description = $"Tenant read key {Guid.NewGuid():N}",
        scopes = new[] { ApiKeyScope.TenantRead }
    });
    createResp.StatusCode.Should().Be(HttpStatusCode.Created);
    var created = await createResp.ReadFromApiJsonAsync<CreateApiKeyResult>();

    // List API keys — requires tenant:read scope → should succeed
    var apiKeyClient = fixture.CreateAnonymousClient();
    apiKeyClient.DefaultRequestHeaders.Add("X-Api-Key", created!.RawKey);

    var listResp = await apiKeyClient.GetAsync(ApiKeysUrl);
    listResp.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

Note: Add `using Chronith.Domain.Models;` to the test file.

**Step 2: Run functional tests**

```bash
dotnet test tests/Chronith.Tests.Functional --filter "ApiKeyEndpointsTests" -v q
```

Expected: PASS (requires Docker for Testcontainers — run `podman machine start` first if not running).

**Step 3: Commit**

```bash
git add tests/Chronith.Tests.Functional/ApiKeys/ApiKeyEndpointsTests.cs
git commit -m "test(functional): update ApiKeyEndpointsTests for scope-based auth"
```

---

## Task 13: Full test suite

**Step 1: Run all tests**

```bash
dotnet test Chronith.slnx -v q
```

Expected: All tests pass.

If any test fails, fix it before continuing.

**Step 2: Fix any remaining references to `Role` in tests**

```bash
grep -r "\.Role\b\|\"role\"\|role\s*=" tests/ --include="*.cs" | grep -v obj/
```

Update any remaining test references from `role` to `scopes`.

**Step 3: Commit fixes if any**

```bash
git add -A
git commit -m "fix: resolve remaining Role references in tests"
```

---

## Task 14: Open PR

**Step 1: Push branch**

```bash
git push -u origin feat/api-key-scopes
```

**Step 2: Open PR**

```bash
gh pr create \
  --title "feat: scope-based RBAC for API keys" \
  --body "$(cat <<'EOF'
## Summary

- Replaces the free-form `Role` string on `TenantApiKey` with a `text[]` `Scopes` column
- Defines 15 well-known scope constants in `ApiKeyScope` (Domain layer)
- Auth handler now emits one `scope` claim per granted scope + synthetic `ApiKey` role claim
- New `ApiKeyScopeRequirement` / `ApiKeyScopeHandler` enforce scope checks on API key callers; JWT callers bypass scope checks
- 15 named authorization policies (`scope:bookings:read`, etc.) registered in `Program.cs`
- `CreateApiKeyCommand` validates all requested scopes against `ApiKeyScope.All`
- Full test coverage: unit (Domain + Application + Infrastructure/Auth), integration (Postgres `text[]` round-trip), functional (create, use, 403 scope-miss)

## Migration

One migration: `AddApiKeyScopesDropRole` — adds `scopes text[]`, drops `role`.

**Existing API keys in deployed environments will have empty scopes after migration and must be re-created.**

## Design doc

`docs/plans/2026-03-21-api-key-scopes-design.md`
EOF
)" \
  --base main
```

---

## Wiring remaining endpoints (follow-up)

The infrastructure is complete after Task 13. Any endpoint that should accept API key callers can be wired following the pattern in Task 11:

```csharp
Roles("TenantAdmin", "TenantStaff", "ApiKey");          // add "ApiKey"
AuthSchemes("Bearer", "ApiKey");                          // accept both
Policies($"scope:{ApiKeyScope.BookingsRead}");            // required scope
```

This can be done incrementally in follow-up PRs as each endpoint needs to support API key auth.
