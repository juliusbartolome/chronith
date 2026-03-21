# Per-Tenant Payment Config — Implementation Plan (v2)

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Allow each tenant to configure multiple named payment provider credentials (PayMongo, Maya, Manual) with per-tenant labels, activate one API config per provider type at a time, and expose active configs publicly. `CreateBookingHandler` uses `ITenantPaymentProviderResolver` — if no active config exists, booking stays at `PendingPayment` for manual admin verification.

**Architecture:** New `TenantPaymentConfig` domain model (mirrors `TenantNotificationConfig`). `ITenantPaymentProviderResolver` (scoped) looks up the per-tenant active config row, decrypts credentials, constructs a fresh provider instance, and returns `null` if no active row exists. `Stub` always resolves (no row required). `Manual` always returns `null`. Global singletons unchanged.

---

## Branch

```bash
git checkout main && git pull
git checkout -b feat/per-tenant-payment-config
```

---

## Task 1: Domain model `TenantPaymentConfig`

**Files:**
- Create: `src/Chronith.Domain/Models/TenantPaymentConfig.cs`
- Create: `tests/Chronith.Tests.Unit/Domain/TenantPaymentConfigTests.cs`

### Step 1: Write failing tests

```csharp
// tests/Chronith.Tests.Unit/Domain/TenantPaymentConfigTests.cs
using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class TenantPaymentConfigTests
{
    [Fact]
    public void Create_ApiType_SetsAllPropertiesAndIsActiveFalse()
    {
        var tenantId = Guid.NewGuid();
        var config = TenantPaymentConfig.Create(
            tenantId, "PayMongo", "MyLabel", """{"SecretKey":"sk_test_abc"}""", null, null);

        config.Id.Should().NotBeEmpty();
        config.TenantId.Should().Be(tenantId);
        config.ProviderName.Should().Be("PayMongo");
        config.Label.Should().Be("MyLabel");
        config.IsActive.Should().BeFalse();
        config.IsDeleted.Should().BeFalse();
        config.Settings.Should().Contain("sk_test_abc");
        config.PublicNote.Should().BeNull();
        config.QrCodeUrl.Should().BeNull();
        config.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        config.UpdatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_ManualType_SetsIsActiveTrue()
    {
        var config = TenantPaymentConfig.Create(
            Guid.NewGuid(), "Manual", "GCash", "{}", "Scan to pay via GCash", "https://qr.example.com/gcash");

        config.IsActive.Should().BeTrue();
        config.PublicNote.Should().Be("Scan to pay via GCash");
        config.QrCodeUrl.Should().Be("https://qr.example.com/gcash");
    }

    [Fact]
    public void UpdateDetails_ChangesFieldsLeavesIsActiveUnchanged()
    {
        var config = TenantPaymentConfig.Create(
            Guid.NewGuid(), "PayMongo", "OldLabel", """{"SecretKey":"sk_old"}""", null, null);
        var originalUpdated = config.UpdatedAt;

        config.UpdateDetails("NewLabel", """{"SecretKey":"sk_new"}""", "note", "https://qr.example.com");

        config.Label.Should().Be("NewLabel");
        config.Settings.Should().Contain("sk_new");
        config.PublicNote.Should().Be("note");
        config.QrCodeUrl.Should().Be("https://qr.example.com");
        config.IsActive.Should().BeFalse(); // unchanged
        config.UpdatedAt.Should().BeOnOrAfter(originalUpdated);
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var config = TenantPaymentConfig.Create(
            Guid.NewGuid(), "PayMongo", "Label", "{}", null, null);
        config.IsActive.Should().BeFalse();

        config.Activate();

        config.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var config = TenantPaymentConfig.Create(
            Guid.NewGuid(), "Manual", "Cash", "{}", null, null);
        config.IsActive.Should().BeTrue();

        config.Deactivate();

        config.IsActive.Should().BeFalse();
    }

    [Fact]
    public void SoftDelete_SetsIsDeletedTrue()
    {
        var config = TenantPaymentConfig.Create(
            Guid.NewGuid(), "PayMongo", "Label", "{}", null, null);
        config.IsDeleted.Should().BeFalse();

        config.SoftDelete();

        config.IsDeleted.Should().BeTrue();
    }
}
```

### Step 2: Run to verify failure

```bash
dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~TenantPaymentConfigTests" --no-build 2>&1 | tail -5
```

### Step 3: Implement

```csharp
// src/Chronith.Domain/Models/TenantPaymentConfig.cs
namespace Chronith.Domain.Models;

public sealed class TenantPaymentConfig
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string ProviderName { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public string Settings { get; private set; } = "{}";
    public string? PublicNote { get; private set; }
    public string? QrCodeUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    internal TenantPaymentConfig() { }

    public static TenantPaymentConfig Create(
        Guid tenantId,
        string providerName,
        string label,
        string settings,
        string? publicNote,
        string? qrCodeUrl)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProviderName = providerName,
            Label = label,
            IsActive = providerName.Equals("Manual", StringComparison.OrdinalIgnoreCase),
            IsDeleted = false,
            Settings = settings,
            PublicNote = publicNote,
            QrCodeUrl = qrCodeUrl,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    public void UpdateDetails(string label, string settings, string? publicNote, string? qrCodeUrl)
    {
        Label = label;
        Settings = settings;
        PublicNote = publicNote;
        QrCodeUrl = qrCodeUrl;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

### Step 4: Run tests — expect pass

```bash
dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~TenantPaymentConfigTests" 2>&1 | tail -5
```

Expected: `Passed: 6`

### Step 5: Commit

```bash
git add src/Chronith.Domain/Models/TenantPaymentConfig.cs \
        tests/Chronith.Tests.Unit/Domain/TenantPaymentConfigTests.cs
git commit -m "feat(domain): add TenantPaymentConfig model"
```

---

## Task 2: Application interfaces, DTOs, mapper

**Files:**
- Create: `src/Chronith.Application/Interfaces/ITenantPaymentConfigRepository.cs`
- Create: `src/Chronith.Application/Interfaces/ITenantPaymentProviderResolver.cs`
- Create: `src/Chronith.Application/DTOs/TenantPaymentConfigDto.cs`
- Create: `src/Chronith.Application/DTOs/PaymentProviderSummaryDto.cs`
- Create: `src/Chronith.Application/Mappers/TenantPaymentConfigMapper.cs`

```csharp
// src/Chronith.Application/Interfaces/ITenantPaymentConfigRepository.cs
using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface ITenantPaymentConfigRepository
{
    Task<TenantPaymentConfig?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TenantPaymentConfig?> GetActiveByProviderNameAsync(Guid tenantId, string providerName, CancellationToken ct = default);
    Task<IReadOnlyList<TenantPaymentConfig>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<TenantPaymentConfig>> ListActiveByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(TenantPaymentConfig config, CancellationToken ct = default);
    Task UpdateAsync(TenantPaymentConfig config, CancellationToken ct = default);
    Task DeactivateAllByProviderNameAsync(Guid tenantId, string providerName, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
}
```

```csharp
// src/Chronith.Application/Interfaces/ITenantPaymentProviderResolver.cs
namespace Chronith.Application.Interfaces;

public interface ITenantPaymentProviderResolver
{
    Task<IPaymentProvider?> ResolveAsync(Guid tenantId, string providerName, CancellationToken ct = default);
}
```

```csharp
// src/Chronith.Application/DTOs/TenantPaymentConfigDto.cs
namespace Chronith.Application.DTOs;

public sealed record TenantPaymentConfigDto(
    Guid Id,
    Guid TenantId,
    string ProviderName,
    string Label,
    bool IsActive,
    string Settings,
    string? PublicNote,
    string? QrCodeUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
```

```csharp
// src/Chronith.Application/DTOs/PaymentProviderSummaryDto.cs
namespace Chronith.Application.DTOs;

public sealed record PaymentProviderSummaryDto(
    Guid Id,
    string ProviderName,
    string Label,
    string? PublicNote,
    string? QrCodeUrl);
```

```csharp
// src/Chronith.Application/Mappers/TenantPaymentConfigMapper.cs
using Chronith.Application.DTOs;
using Chronith.Domain.Models;

namespace Chronith.Application.Mappers;

public static class TenantPaymentConfigMapper
{
    public static TenantPaymentConfigDto ToDto(this TenantPaymentConfig config) => new(
        config.Id,
        config.TenantId,
        config.ProviderName,
        config.Label,
        config.IsActive,
        config.Settings,
        config.PublicNote,
        config.QrCodeUrl,
        config.CreatedAt,
        config.UpdatedAt);

    public static PaymentProviderSummaryDto ToSummaryDto(this TenantPaymentConfig config) => new(
        config.Id,
        config.ProviderName,
        config.Label,
        config.PublicNote,
        config.QrCodeUrl);
}
```

Build: `dotnet build src/Chronith.Application --no-incremental 2>&1 | tail -5`

Commit: `git commit -m "feat(application): add ITenantPaymentConfigRepository, resolver interface, DTOs, mapper"`

---

## Task 3: `CreateTenantPaymentConfigCommand`

**Files:**
- Create: `src/Chronith.Application/Commands/TenantPaymentConfig/CreateTenantPaymentConfigCommand.cs`
- Create: `tests/Chronith.Tests.Unit/Application/CreateTenantPaymentConfigCommandHandlerTests.cs`

### Tests

```csharp
using Chronith.Application.Commands.TenantPaymentConfig;
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class CreateTenantPaymentConfigCommandHandlerTests
{
    private readonly ITenantPaymentConfigRepository _repo = Substitute.For<ITenantPaymentConfigRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private static readonly Guid TenantId = Guid.Parse("11111111-0000-0000-0000-000000000001");

    public CreateTenantPaymentConfigCommandHandlerTests() => _tenantContext.TenantId.Returns(TenantId);

    [Fact]
    public async Task Handle_ApiType_CreatesConfigWithIsActiveFalse()
    {
        var handler = new CreateTenantPaymentConfigCommandHandler(_repo, _tenantContext, _unitOfWork);
        var cmd = new CreateTenantPaymentConfigCommand
        {
            ProviderName = "PayMongo",
            Label = "PayMongo Dev",
            Settings = """{"SecretKey":"sk_test"}""",
            PublicNote = null,
            QrCodeUrl = null
        };

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.ProviderName.Should().Be("PayMongo");
        result.Label.Should().Be("PayMongo Dev");
        result.IsActive.Should().BeFalse();
        await _repo.Received(1).AddAsync(Arg.Any<TenantPaymentConfig>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ManualType_CreatesConfigWithIsActiveTrue()
    {
        var handler = new CreateTenantPaymentConfigCommandHandler(_repo, _tenantContext, _unitOfWork);
        var cmd = new CreateTenantPaymentConfigCommand
        {
            ProviderName = "Manual",
            Label = "GCash",
            Settings = "{}",
            PublicNote = "Scan to pay",
            QrCodeUrl = "https://qr.example.com"
        };

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsActive.Should().BeTrue();
        result.PublicNote.Should().Be("Scan to pay");
        result.QrCodeUrl.Should().Be("https://qr.example.com");
    }
}
```

### Implementation

```csharp
// src/Chronith.Application/Commands/TenantPaymentConfig/CreateTenantPaymentConfigCommand.cs
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.TenantPaymentConfig;

public sealed record CreateTenantPaymentConfigCommand : IRequest<TenantPaymentConfigDto>, IAuditable
{
    public required string ProviderName { get; init; }
    public required string Label { get; init; }
    public required string Settings { get; init; }
    public string? PublicNote { get; init; }
    public string? QrCodeUrl { get; init; }

    public Guid EntityId => Guid.Empty;
    public string EntityType => "TenantPaymentConfig";
    public string Action => "Create";
}

public sealed class CreateTenantPaymentConfigCommandValidator
    : AbstractValidator<CreateTenantPaymentConfigCommand>
{
    private static readonly string[] ValidProviders = ["PayMongo", "Maya", "Manual", "Stub"];

    public CreateTenantPaymentConfigCommandValidator()
    {
        RuleFor(x => x.ProviderName)
            .NotEmpty()
            .MaximumLength(50)
            .Must(p => ValidProviders.Contains(p, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"ProviderName must be one of: {string.Join(", ", ValidProviders)}");

        RuleFor(x => x.Label).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Settings).NotEmpty().MaximumLength(4096);
        RuleFor(x => x.PublicNote).MaximumLength(500).When(x => x.PublicNote is not null);
        RuleFor(x => x.QrCodeUrl).MaximumLength(2048).When(x => x.QrCodeUrl is not null);
    }
}

public sealed class CreateTenantPaymentConfigCommandHandler(
    ITenantPaymentConfigRepository repo,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateTenantPaymentConfigCommand, TenantPaymentConfigDto>
{
    public async Task<TenantPaymentConfigDto> Handle(
        CreateTenantPaymentConfigCommand cmd, CancellationToken ct)
    {
        var config = TenantPaymentConfig.Create(
            tenantContext.TenantId,
            cmd.ProviderName,
            cmd.Label,
            cmd.Settings,
            cmd.PublicNote,
            cmd.QrCodeUrl);

        await repo.AddAsync(config, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return config.ToDto();
    }
}
```

Verify: `dotnet test tests/Chronith.Tests.Unit --filter "FullyQualifiedName~CreateTenantPaymentConfigCommandHandlerTests" 2>&1 | tail -5`

Commit: `git commit -m "feat(application): add CreateTenantPaymentConfigCommand"`

---

## Task 4: `UpdateTenantPaymentConfigCommand`

**Files:**
- Create: `src/Chronith.Application/Commands/TenantPaymentConfig/UpdateTenantPaymentConfigCommand.cs`
- Create: `tests/Chronith.Tests.Unit/Application/UpdateTenantPaymentConfigCommandHandlerTests.cs`

### Tests

```csharp
using Chronith.Application.Commands.TenantPaymentConfig;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class UpdateTenantPaymentConfigCommandHandlerTests
{
    private readonly ITenantPaymentConfigRepository _repo = Substitute.For<ITenantPaymentConfigRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Handle_WhenExists_UpdatesAndReturnsDto()
    {
        var id = Guid.NewGuid();
        var existing = TenantPaymentConfig.Create(Guid.NewGuid(), "PayMongo", "OldLabel", "{}", null, null);
        _repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(existing);

        var handler = new UpdateTenantPaymentConfigCommandHandler(_repo, _unitOfWork);
        var cmd = new UpdateTenantPaymentConfigCommand
        {
            Id = id,
            Label = "NewLabel",
            Settings = """{"SecretKey":"sk_new"}""",
            PublicNote = null,
            QrCodeUrl = null
        };

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.Label.Should().Be("NewLabel");
        result.Settings.Should().Contain("sk_new");
        result.IsActive.Should().BeFalse(); // unchanged
        await _repo.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotFound_ThrowsNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns((TenantPaymentConfig?)null);

        var handler = new UpdateTenantPaymentConfigCommandHandler(_repo, _unitOfWork);
        var act = () => handler.Handle(
            new UpdateTenantPaymentConfigCommand
            {
                Id = Guid.NewGuid(), Label = "L", Settings = "{}", PublicNote = null, QrCodeUrl = null
            }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
```

### Implementation

```csharp
// src/Chronith.Application/Commands/TenantPaymentConfig/UpdateTenantPaymentConfigCommand.cs
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.TenantPaymentConfig;

public sealed record UpdateTenantPaymentConfigCommand : IRequest<TenantPaymentConfigDto>, IAuditable
{
    public required Guid Id { get; init; }
    public required string Label { get; init; }
    public required string Settings { get; init; }
    public string? PublicNote { get; init; }
    public string? QrCodeUrl { get; init; }

    public Guid EntityId => Id;
    public string EntityType => "TenantPaymentConfig";
    public string Action => "Update";
}

public sealed class UpdateTenantPaymentConfigCommandValidator
    : AbstractValidator<UpdateTenantPaymentConfigCommand>
{
    public UpdateTenantPaymentConfigCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Label).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Settings).NotEmpty().MaximumLength(4096);
        RuleFor(x => x.PublicNote).MaximumLength(500).When(x => x.PublicNote is not null);
        RuleFor(x => x.QrCodeUrl).MaximumLength(2048).When(x => x.QrCodeUrl is not null);
    }
}

public sealed class UpdateTenantPaymentConfigCommandHandler(
    ITenantPaymentConfigRepository repo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateTenantPaymentConfigCommand, TenantPaymentConfigDto>
{
    public async Task<TenantPaymentConfigDto> Handle(
        UpdateTenantPaymentConfigCommand cmd, CancellationToken ct)
    {
        var config = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException("TenantPaymentConfig", cmd.Id);

        config.UpdateDetails(cmd.Label, cmd.Settings, cmd.PublicNote, cmd.QrCodeUrl);
        await repo.UpdateAsync(config, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return config.ToDto();
    }
}
```

Commit: `git commit -m "feat(application): add UpdateTenantPaymentConfigCommand"`

---

## Task 5: `ActivateTenantPaymentConfigCommand`

**Files:**
- Create: `src/Chronith.Application/Commands/TenantPaymentConfig/ActivateTenantPaymentConfigCommand.cs`
- Create: `tests/Chronith.Tests.Unit/Application/ActivateTenantPaymentConfigCommandHandlerTests.cs`

### Tests

```csharp
using Chronith.Application.Commands.TenantPaymentConfig;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class ActivateTenantPaymentConfigCommandHandlerTests
{
    private readonly ITenantPaymentConfigRepository _repo = Substitute.For<ITenantPaymentConfigRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Handle_ApiType_DeactivatesOthersFirstThenActivates()
    {
        var id = Guid.NewGuid();
        var config = TenantPaymentConfig.Create(Guid.NewGuid(), "PayMongo", "Label", "{}", null, null);
        _repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(config);

        var handler = new ActivateTenantPaymentConfigCommandHandler(_repo, _unitOfWork);
        await handler.Handle(new ActivateTenantPaymentConfigCommand(id), CancellationToken.None);

        await _repo.Received(1).DeactivateAllByProviderNameAsync(
            config.TenantId, "PayMongo", Arg.Any<CancellationToken>());
        config.IsActive.Should().BeTrue();
        await _repo.Received(1).UpdateAsync(config, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ManualType_ActivatesWithoutDeactivatingOthers()
    {
        var id = Guid.NewGuid();
        var config = TenantPaymentConfig.Create(Guid.NewGuid(), "Manual", "Cash", "{}", null, null);
        config.Deactivate(); // start inactive
        _repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(config);

        var handler = new ActivateTenantPaymentConfigCommandHandler(_repo, _unitOfWork);
        await handler.Handle(new ActivateTenantPaymentConfigCommand(id), CancellationToken.None);

        await _repo.DidNotReceive().DeactivateAllByProviderNameAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        config.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenNotFound_ThrowsNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns((TenantPaymentConfig?)null);

        var handler = new ActivateTenantPaymentConfigCommandHandler(_repo, _unitOfWork);
        var act = () => handler.Handle(
            new ActivateTenantPaymentConfigCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
```

### Implementation

```csharp
// src/Chronith.Application/Commands/TenantPaymentConfig/ActivateTenantPaymentConfigCommand.cs
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Commands.TenantPaymentConfig;

public sealed record ActivateTenantPaymentConfigCommand(Guid Id) : IRequest, IAuditable
{
    public Guid EntityId => Id;
    public string EntityType => "TenantPaymentConfig";
    public string Action => "Activate";
}

public sealed class ActivateTenantPaymentConfigCommandHandler(
    ITenantPaymentConfigRepository repo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ActivateTenantPaymentConfigCommand>
{
    public async Task Handle(ActivateTenantPaymentConfigCommand cmd, CancellationToken ct)
    {
        var config = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException("TenantPaymentConfig", cmd.Id);

        // For API types, ensure only one active at a time
        if (!config.ProviderName.Equals("Manual", StringComparison.OrdinalIgnoreCase))
            await repo.DeactivateAllByProviderNameAsync(config.TenantId, config.ProviderName, ct);

        config.Activate();
        await repo.UpdateAsync(config, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
```

Commit: `git commit -m "feat(application): add ActivateTenantPaymentConfigCommand"`

---

## Task 6: `DeactivateTenantPaymentConfigCommand`

**Files:**
- Create: `src/Chronith.Application/Commands/TenantPaymentConfig/DeactivateTenantPaymentConfigCommand.cs`
- Create: `tests/Chronith.Tests.Unit/Application/DeactivateTenantPaymentConfigCommandHandlerTests.cs`

### Tests

```csharp
using Chronith.Application.Commands.TenantPaymentConfig;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class DeactivateTenantPaymentConfigCommandHandlerTests
{
    private readonly ITenantPaymentConfigRepository _repo = Substitute.For<ITenantPaymentConfigRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Handle_DeactivatesConfig()
    {
        var id = Guid.NewGuid();
        var config = TenantPaymentConfig.Create(Guid.NewGuid(), "Manual", "Cash", "{}", null, null);
        config.Activate();
        _repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(config);

        var handler = new DeactivateTenantPaymentConfigCommandHandler(_repo, _unitOfWork);
        await handler.Handle(new DeactivateTenantPaymentConfigCommand(id), CancellationToken.None);

        config.IsActive.Should().BeFalse();
        await _repo.Received(1).UpdateAsync(config, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotFound_ThrowsNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns((TenantPaymentConfig?)null);

        var handler = new DeactivateTenantPaymentConfigCommandHandler(_repo, _unitOfWork);
        var act = () => handler.Handle(
            new DeactivateTenantPaymentConfigCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
```

### Implementation

```csharp
// src/Chronith.Application/Commands/TenantPaymentConfig/DeactivateTenantPaymentConfigCommand.cs
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Commands.TenantPaymentConfig;

public sealed record DeactivateTenantPaymentConfigCommand(Guid Id) : IRequest, IAuditable
{
    public Guid EntityId => Id;
    public string EntityType => "TenantPaymentConfig";
    public string Action => "Deactivate";
}

public sealed class DeactivateTenantPaymentConfigCommandHandler(
    ITenantPaymentConfigRepository repo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeactivateTenantPaymentConfigCommand>
{
    public async Task Handle(DeactivateTenantPaymentConfigCommand cmd, CancellationToken ct)
    {
        var config = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException("TenantPaymentConfig", cmd.Id);

        config.Deactivate();
        await repo.UpdateAsync(config, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
```

Commit: `git commit -m "feat(application): add DeactivateTenantPaymentConfigCommand"`

---

## Task 7: `DeleteTenantPaymentConfigCommand` (soft delete)

**Files:**
- Create: `src/Chronith.Application/Commands/TenantPaymentConfig/DeleteTenantPaymentConfigCommand.cs`
- Create: `tests/Chronith.Tests.Unit/Application/DeleteTenantPaymentConfigCommandHandlerTests.cs`

### Tests

```csharp
using Chronith.Application.Commands.TenantPaymentConfig;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class DeleteTenantPaymentConfigCommandHandlerTests
{
    private readonly ITenantPaymentConfigRepository _repo = Substitute.For<ITenantPaymentConfigRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Handle_WhenExists_SoftDeletes()
    {
        var id = Guid.NewGuid();
        var config = TenantPaymentConfig.Create(Guid.NewGuid(), "PayMongo", "Label", "{}", null, null);
        _repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(config);

        var handler = new DeleteTenantPaymentConfigCommandHandler(_repo, _unitOfWork);
        await handler.Handle(new DeleteTenantPaymentConfigCommand(id), CancellationToken.None);

        await _repo.Received(1).SoftDeleteAsync(id, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotFound_ThrowsNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns((TenantPaymentConfig?)null);

        var handler = new DeleteTenantPaymentConfigCommandHandler(_repo, _unitOfWork);
        var act = () => handler.Handle(
            new DeleteTenantPaymentConfigCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
```

### Implementation

```csharp
// src/Chronith.Application/Commands/TenantPaymentConfig/DeleteTenantPaymentConfigCommand.cs
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using MediatR;

namespace Chronith.Application.Commands.TenantPaymentConfig;

public sealed record DeleteTenantPaymentConfigCommand(Guid Id) : IRequest, IAuditable
{
    public Guid EntityId => Id;
    public string EntityType => "TenantPaymentConfig";
    public string Action => "Delete";
}

public sealed class DeleteTenantPaymentConfigCommandHandler(
    ITenantPaymentConfigRepository repo,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteTenantPaymentConfigCommand>
{
    public async Task Handle(DeleteTenantPaymentConfigCommand cmd, CancellationToken ct)
    {
        _ = await repo.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException("TenantPaymentConfig", cmd.Id);

        await repo.SoftDeleteAsync(cmd.Id, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
```

Commit: `git commit -m "feat(application): add DeleteTenantPaymentConfigCommand (soft delete)"`

---

## Task 8: `GetTenantPaymentConfigsQuery`

**Files:**
- Create: `src/Chronith.Application/Queries/TenantPaymentConfig/GetTenantPaymentConfigsQuery.cs`
- Create: `tests/Chronith.Tests.Unit/Application/GetTenantPaymentConfigsQueryHandlerTests.cs`

### Tests

```csharp
using Chronith.Application.Interfaces;
using Chronith.Application.Queries.TenantPaymentConfig;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class GetTenantPaymentConfigsQueryHandlerTests
{
    private readonly ITenantPaymentConfigRepository _repo = Substitute.For<ITenantPaymentConfigRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private static readonly Guid TenantId = Guid.Parse("22222222-0000-0000-0000-000000000001");

    public GetTenantPaymentConfigsQueryHandlerTests() => _tenantContext.TenantId.Returns(TenantId);

    [Fact]
    public async Task Handle_ReturnsAllNonDeletedConfigsForTenant()
    {
        var configs = new List<TenantPaymentConfig>
        {
            TenantPaymentConfig.Create(TenantId, "PayMongo", "Dev", """{"SecretKey":"sk_1"}""", null, null),
            TenantPaymentConfig.Create(TenantId, "Manual", "GCash", "{}", "Pay via GCash", null)
        };
        _repo.ListByTenantAsync(TenantId, Arg.Any<CancellationToken>()).Returns(configs);

        var handler = new GetTenantPaymentConfigsQueryHandler(_repo, _tenantContext);
        var result = await handler.Handle(new GetTenantPaymentConfigsQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(r => r.ProviderName).Should().Contain(["PayMongo", "Manual"]);
    }

    [Fact]
    public async Task Handle_WhenNoConfigs_ReturnsEmptyList()
    {
        _repo.ListByTenantAsync(TenantId, Arg.Any<CancellationToken>())
             .Returns(new List<TenantPaymentConfig>());

        var handler = new GetTenantPaymentConfigsQueryHandler(_repo, _tenantContext);
        var result = await handler.Handle(new GetTenantPaymentConfigsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
```

### Implementation

```csharp
// src/Chronith.Application/Queries/TenantPaymentConfig/GetTenantPaymentConfigsQuery.cs
using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using MediatR;

namespace Chronith.Application.Queries.TenantPaymentConfig;

public sealed record GetTenantPaymentConfigsQuery
    : IRequest<IReadOnlyList<TenantPaymentConfigDto>>, IQuery;

public sealed class GetTenantPaymentConfigsQueryHandler(
    ITenantPaymentConfigRepository repo,
    ITenantContext tenantContext)
    : IRequestHandler<GetTenantPaymentConfigsQuery, IReadOnlyList<TenantPaymentConfigDto>>
{
    public async Task<IReadOnlyList<TenantPaymentConfigDto>> Handle(
        GetTenantPaymentConfigsQuery query, CancellationToken ct)
    {
        var configs = await repo.ListByTenantAsync(tenantContext.TenantId, ct);
        return configs.Select(c => c.ToDto()).ToList();
    }
}
```

Commit: `git commit -m "feat(application): add GetTenantPaymentConfigsQuery"`

---

## Task 9: `GetPublicPaymentProvidersQuery`

**Files:**
- Create: `src/Chronith.Application/Queries/TenantPaymentConfig/GetPublicPaymentProvidersQuery.cs`
- Create: `tests/Chronith.Tests.Unit/Application/GetPublicPaymentProvidersQueryHandlerTests.cs`

### Tests

```csharp
using Chronith.Application.Interfaces;
using Chronith.Application.Queries.TenantPaymentConfig;
using Chronith.Domain.Models;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class GetPublicPaymentProvidersQueryHandlerTests
{
    private readonly ITenantPaymentConfigRepository _repo = Substitute.For<ITenantPaymentConfigRepository>();

    [Fact]
    public async Task Handle_ReturnsOnlyActiveConfigs_AsSummaryDtos()
    {
        var tenantId = Guid.NewGuid();
        var active = TenantPaymentConfig.Create(tenantId, "Manual", "GCash", "{}", "Pay via GCash", "https://qr.example.com");
        _repo.ListActiveByTenantAsync(tenantId, Arg.Any<CancellationToken>()).Returns([active]);

        var handler = new GetPublicPaymentProvidersQueryHandler(_repo);
        var result = await handler.Handle(new GetPublicPaymentProvidersQuery(tenantId), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].ProviderName.Should().Be("Manual");
        result[0].Label.Should().Be("GCash");
        result[0].PublicNote.Should().Be("Pay via GCash");
    }

    [Fact]
    public async Task Handle_WhenNoneActive_ReturnsEmpty()
    {
        _repo.ListActiveByTenantAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
             .Returns([]);

        var handler = new GetPublicPaymentProvidersQueryHandler(_repo);
        var result = await handler.Handle(
            new GetPublicPaymentProvidersQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
```

### Implementation

```csharp
// src/Chronith.Application/Queries/TenantPaymentConfig/GetPublicPaymentProvidersQuery.cs
using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using MediatR;

namespace Chronith.Application.Queries.TenantPaymentConfig;

public sealed record GetPublicPaymentProvidersQuery(Guid TenantId)
    : IRequest<IReadOnlyList<PaymentProviderSummaryDto>>, IQuery;

public sealed class GetPublicPaymentProvidersQueryHandler(
    ITenantPaymentConfigRepository repo)
    : IRequestHandler<GetPublicPaymentProvidersQuery, IReadOnlyList<PaymentProviderSummaryDto>>
{
    public async Task<IReadOnlyList<PaymentProviderSummaryDto>> Handle(
        GetPublicPaymentProvidersQuery query, CancellationToken ct)
    {
        var configs = await repo.ListActiveByTenantAsync(query.TenantId, ct);
        return configs.Select(c => c.ToSummaryDto()).ToList();
    }
}
```

Commit: `git commit -m "feat(application): add GetPublicPaymentProvidersQuery"`

---

## Task 10: Infrastructure — entity, EF config, mapper, DbSet

**Files:**
- Create: `src/Chronith.Infrastructure/Persistence/Entities/TenantPaymentConfigEntity.cs`
- Create: `src/Chronith.Infrastructure/Persistence/Configurations/TenantPaymentConfigConfiguration.cs`
- Create: `src/Chronith.Infrastructure/Persistence/Mappers/TenantPaymentConfigEntityMapper.cs`
- Modify: `src/Chronith.Infrastructure/Persistence/ChronithDbContext.cs`

**Entity:**

```csharp
namespace Chronith.Infrastructure.Persistence.Entities;

public sealed class TenantPaymentConfigEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public string Settings { get; set; } = "{}";
    public string? PublicNote { get; set; }
    public string? QrCodeUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
```

**EF Configuration:**

```csharp
using Chronith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chronith.Infrastructure.Persistence.Configurations;

public sealed class TenantPaymentConfigConfiguration
    : IEntityTypeConfiguration<TenantPaymentConfigEntity>
{
    public void Configure(EntityTypeBuilder<TenantPaymentConfigEntity> builder)
    {
        builder.ToTable("tenant_payment_configs", "chronith");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.ProviderName).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Label).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Settings).HasColumnType("text").IsRequired();
        builder.Property(c => c.PublicNote).HasColumnType("text");
        builder.Property(c => c.QrCodeUrl).HasColumnType("text");

        // Unique label per tenant+provider (excluding deleted)
        builder.HasIndex(c => new { c.TenantId, c.ProviderName, c.Label })
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("IX_tenant_payment_configs_TenantId_ProviderName_Label");

        // Only one active API config per tenant+provider (Manual allows multiple)
        builder.HasIndex(c => new { c.TenantId, c.ProviderName })
            .IsUnique()
            .HasFilter("is_active = true AND is_deleted = false AND provider_name != 'Manual'")
            .HasDatabaseName("IX_tenant_payment_configs_TenantId_ProviderName_active");

        builder.HasIndex(c => c.TenantId)
            .HasDatabaseName("IX_tenant_payment_configs_TenantId");
    }
}
```

**Entity Mapper:**

```csharp
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;

namespace Chronith.Infrastructure.Persistence.Mappers;

public static class TenantPaymentConfigEntityMapper
{
    public static TenantPaymentConfig ToDomain(TenantPaymentConfigEntity entity)
    {
        var domain = new TenantPaymentConfig();
        SetProperty(domain, nameof(TenantPaymentConfig.Id), entity.Id);
        SetProperty(domain, nameof(TenantPaymentConfig.TenantId), entity.TenantId);
        SetProperty(domain, nameof(TenantPaymentConfig.ProviderName), entity.ProviderName);
        SetProperty(domain, nameof(TenantPaymentConfig.Label), entity.Label);
        SetProperty(domain, nameof(TenantPaymentConfig.IsActive), entity.IsActive);
        SetProperty(domain, nameof(TenantPaymentConfig.IsDeleted), entity.IsDeleted);
        SetProperty(domain, nameof(TenantPaymentConfig.Settings), entity.Settings);
        SetProperty(domain, nameof(TenantPaymentConfig.PublicNote), entity.PublicNote);
        SetProperty(domain, nameof(TenantPaymentConfig.QrCodeUrl), entity.QrCodeUrl);
        SetProperty(domain, nameof(TenantPaymentConfig.CreatedAt), entity.CreatedAt);
        SetProperty(domain, nameof(TenantPaymentConfig.UpdatedAt), entity.UpdatedAt);
        return domain;
    }

    public static TenantPaymentConfigEntity ToEntity(TenantPaymentConfig domain) => new()
    {
        Id = domain.Id,
        TenantId = domain.TenantId,
        ProviderName = domain.ProviderName,
        Label = domain.Label,
        IsActive = domain.IsActive,
        IsDeleted = domain.IsDeleted,
        Settings = domain.Settings,
        PublicNote = domain.PublicNote,
        QrCodeUrl = domain.QrCodeUrl,
        CreatedAt = domain.CreatedAt,
        UpdatedAt = domain.UpdatedAt
    };

    private static void SetProperty<T>(object target, string propertyName, T value)
    {
        var prop = target.GetType().GetProperty(propertyName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        prop?.SetValue(target, value);
    }
}
```

**DbSet addition** — in `ChronithDbContext.cs`, after `TenantNotificationConfigs` DbSet:

```csharp
// No global query filter: accessed via ITenantPaymentProviderResolver which filters explicitly
public DbSet<TenantPaymentConfigEntity> TenantPaymentConfigs => Set<TenantPaymentConfigEntity>();
```

Build: `dotnet build src/Chronith.Infrastructure --no-incremental 2>&1 | tail -5`

Commit: `git commit -m "feat(infra): add TenantPaymentConfig entity, EF config, mapper, DbSet"`

---

## Task 11: `TenantPaymentConfigRepository` + integration tests

**Files:**
- Create: `src/Chronith.Infrastructure/Persistence/Repositories/TenantPaymentConfigRepository.cs`
- Create: `tests/Chronith.Tests.Integration/Persistence/TenantPaymentConfigRepositoryTests.cs`

**Repository implementation:**

```csharp
using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class TenantPaymentConfigRepository(
    ChronithDbContext db,
    IEncryptionService encryptionService)
    : ITenantPaymentConfigRepository
{
    public async Task<TenantPaymentConfig?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.TenantPaymentConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        if (entity is null) return null;
        entity.Settings = DecryptSettings(entity.Settings);
        return TenantPaymentConfigEntityMapper.ToDomain(entity);
    }

    public async Task<TenantPaymentConfig?> GetActiveByProviderNameAsync(
        Guid tenantId, string providerName, CancellationToken ct = default)
    {
        var entity = await db.TenantPaymentConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.TenantId == tenantId &&
                c.ProviderName == providerName &&
                c.IsActive &&
                !c.IsDeleted, ct);
        if (entity is null) return null;
        entity.Settings = DecryptSettings(entity.Settings);
        return TenantPaymentConfigEntityMapper.ToDomain(entity);
    }

    public async Task<IReadOnlyList<TenantPaymentConfig>> ListByTenantAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var entities = await db.TenantPaymentConfigs
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .OrderBy(c => c.ProviderName).ThenBy(c => c.Label)
            .ToListAsync(ct);
        foreach (var e in entities) e.Settings = DecryptSettings(e.Settings);
        return entities.Select(TenantPaymentConfigEntityMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<TenantPaymentConfig>> ListActiveByTenantAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var entities = await db.TenantPaymentConfigs
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.IsActive && !c.IsDeleted)
            .OrderBy(c => c.ProviderName).ThenBy(c => c.Label)
            .ToListAsync(ct);
        // Public endpoint — settings not exposed, but decrypt anyway for consistency
        foreach (var e in entities) e.Settings = DecryptSettings(e.Settings);
        return entities.Select(TenantPaymentConfigEntityMapper.ToDomain).ToList();
    }

    public async Task AddAsync(TenantPaymentConfig config, CancellationToken ct = default)
    {
        var entity = TenantPaymentConfigEntityMapper.ToEntity(config);
        entity.Settings = encryptionService.Encrypt(config.Settings) ?? "{}";
        await db.TenantPaymentConfigs.AddAsync(entity, ct);
    }

    public async Task UpdateAsync(TenantPaymentConfig config, CancellationToken ct = default)
    {
        var encryptedSettings = encryptionService.Encrypt(config.Settings) ?? "{}";
        await db.TenantPaymentConfigs
            .Where(c => c.Id == config.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Label, config.Label)
                .SetProperty(c => c.IsActive, config.IsActive)
                .SetProperty(c => c.IsDeleted, config.IsDeleted)
                .SetProperty(c => c.Settings, encryptedSettings)
                .SetProperty(c => c.PublicNote, config.PublicNote)
                .SetProperty(c => c.QrCodeUrl, config.QrCodeUrl)
                .SetProperty(c => c.UpdatedAt, config.UpdatedAt),
                ct);
    }

    public async Task DeactivateAllByProviderNameAsync(
        Guid tenantId, string providerName, CancellationToken ct = default)
    {
        await db.TenantPaymentConfigs
            .Where(c => c.TenantId == tenantId && c.ProviderName == providerName && !c.IsDeleted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IsActive, false)
                .SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow),
                ct);
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        await db.TenantPaymentConfigs
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.IsDeleted, true)
                .SetProperty(c => c.IsActive, false)
                .SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow),
                ct);
    }

    private string DecryptSettings(string? settings)
    {
        if (settings is null) return "{}";
        try { return encryptionService.Decrypt(settings) ?? "{}"; }
        catch (FormatException) { return settings; }
    }
}
```

**Integration tests** — key scenarios:
1. `AddAsync + GetByIdAsync` returns decrypted config
2. `AddAsync + GetActiveByProviderNameAsync` returns null when IsActive=false
3. `UpdateAsync` changes label/settings
4. `DeactivateAllByProviderNameAsync` sets all matching IsActive=false
5. `SoftDeleteAsync` makes GetByIdAsync return null
6. `ListByTenantAsync` excludes soft-deleted
7. `ListActiveByTenantAsync` returns only active
8. Settings encrypted at rest

Commit: `git commit -m "feat(infra): add TenantPaymentConfigRepository with encryption"`

---

## Task 12: EF migration

```bash
dotnet ef migrations add AddTenantPaymentConfigs \
  --project src/Chronith.Infrastructure \
  --startup-project src/Chronith.API \
  --output-dir Migrations/PostgreSQL
```

Verify migration file contains:
- Table `tenant_payment_configs` in `chronith` schema
- All columns including `Label`, `IsActive`, `IsDeleted`, `PublicNote`, `QrCodeUrl`
- Partial unique index with filter

Commit: `git commit -m "feat(infra): migration AddTenantPaymentConfigs"`

---

## Task 13: `TenantPaymentProviderResolver`

**Files:**
- Create: `src/Chronith.Infrastructure/Payments/TenantPaymentProviderResolver.cs`
- Create: `tests/Chronith.Tests.Unit/Infrastructure/TenantPaymentProviderResolverTests.cs`

**Resolver behavior:**
- `Stub` → always returns `StubPaymentProvider`, no DB lookup
- `Manual` → always returns `null`, no DB lookup
- `PayMongo` / `Maya` → calls `GetActiveByProviderNameAsync`, returns `null` if no active row
- Unknown → returns `null`

```csharp
using System.Text.Json;
using Chronith.Application.Interfaces;
using Chronith.Infrastructure.Payments.PayMongo;
using Microsoft.Extensions.Options;

namespace Chronith.Infrastructure.Payments;

public sealed class TenantPaymentProviderResolver(
    ITenantPaymentConfigRepository configRepo,
    IHttpClientFactory httpClientFactory)
    : ITenantPaymentProviderResolver
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<IPaymentProvider?> ResolveAsync(
        Guid tenantId, string providerName, CancellationToken ct = default)
    {
        if (providerName.Equals("Stub", StringComparison.OrdinalIgnoreCase))
            return new StubPaymentProvider();

        if (providerName.Equals("Manual", StringComparison.OrdinalIgnoreCase))
            return null;

        var config = await configRepo.GetActiveByProviderNameAsync(tenantId, providerName, ct);
        if (config is null) return null;

        return providerName.ToUpperInvariant() switch
        {
            "PAYMONGO" => BuildPayMongo(config.Settings),
            "MAYA"     => BuildMaya(config.Settings),
            _          => null
        };
    }

    private IPaymentProvider BuildPayMongo(string settings)
    {
        var opts = JsonSerializer.Deserialize<PayMongoOptions>(settings, JsonOpts) ?? new PayMongoOptions();
        return new PayMongoProvider(Options.Create(opts), httpClientFactory);
    }

    private IPaymentProvider BuildMaya(string settings)
    {
        var opts = JsonSerializer.Deserialize<MayaOptions>(settings, JsonOpts) ?? new MayaOptions();
        return new MayaProvider(Options.Create(opts), httpClientFactory);
    }
}
```

**Tests:** Stub always resolves; Manual always null; PayMongo/Maya with active config resolves; PayMongo with no active config returns null.

Commit: `git commit -m "feat(infra): add TenantPaymentProviderResolver"`

---

## Task 14: DI wiring

In `src/Chronith.Infrastructure/DependencyInjection.cs`, after the `INotificationConfigRepository` line, add:

```csharp
services.AddScoped<ITenantPaymentConfigRepository, TenantPaymentConfigRepository>();
services.AddScoped<ITenantPaymentProviderResolver, TenantPaymentProviderResolver>();
```

Build full solution: `dotnet build Chronith.slnx --no-incremental 2>&1 | tail -10`

Run unit tests: `dotnet test tests/Chronith.Tests.Unit --no-build 2>&1 | tail -5`

Commit: `git commit -m "feat(infra): register ITenantPaymentConfigRepository and ITenantPaymentProviderResolver"`

---

## Task 15: Admin API endpoints (6 endpoints)

**File:** `src/Chronith.API/Endpoints/Tenant/TenantPaymentConfigEndpoints.cs`

Six endpoints in one file:

| Method | Route | Handler |
|--------|-------|---------|
| GET | `/tenant/payment-config` | `GetTenantPaymentConfigsQuery` |
| POST | `/tenant/payment-config` | `CreateTenantPaymentConfigCommand` |
| PUT | `/tenant/payment-config/{id}` | `UpdateTenantPaymentConfigCommand` |
| DELETE | `/tenant/payment-config/{id}` | `DeleteTenantPaymentConfigCommand` |
| PATCH | `/tenant/payment-config/{id}/activate` | `ActivateTenantPaymentConfigCommand` |
| PATCH | `/tenant/payment-config/{id}/deactivate` | `DeactivateTenantPaymentConfigCommand` |

All: `Roles("TenantAdmin")`, `RequireRateLimiting("Authenticated")`, `WithTags("Tenant")`

Build: `dotnet build src/Chronith.API --no-incremental 2>&1 | tail -5`

Commit: `git commit -m "feat(api): add admin payment config endpoints"`

---

## Task 16: Public endpoint

**File:** `src/Chronith.API/Endpoints/Public/PublicGetPaymentProvidersEndpoint.cs`

Pattern from `PublicListBookingTypesEndpoint`:
- Inject `ITenantRepository` in constructor
- `AllowAnonymous()`, `RequireRateLimiting("Public")`, `WithTags("Public")`
- Route: `GET /public/{tenantSlug}/payment-providers`
- Resolve tenant by slug → `GetPublicPaymentProvidersQuery(tenant.Id)`
- Return 404 if tenant not found

Commit: `git commit -m "feat(api): add public GET /public/{tenantSlug}/payment-providers endpoint"`

---

## Task 17: Update `CreateBookingHandler`

**Files:**
- Modify: `src/Chronith.Application/Commands/Bookings/CreateBookingCommand.cs`
- Modify: `tests/Chronith.Tests.Unit/Application/CreateBookingHandlerTests.cs`

In handler constructor, replace `IPaymentProviderFactory paymentProviderFactory` with `ITenantPaymentProviderResolver tenantPaymentProviderResolver`.

Replace payment block (synchronous `GetProvider`) with:

```csharp
var provider = await tenantPaymentProviderResolver.ResolveAsync(
    tenantContext.TenantId, providerName, ct);

if (provider is not null)
{
    var checkoutResult = await provider.CreateCheckoutSessionAsync(..., ct);
    booking.SetCheckoutDetails(checkoutResult.CheckoutUrl, checkoutResult.ProviderTransactionId);
    metrics.RecordPaymentProcessed(tenantContext.TenantId.ToString(), providerName);
    await bookingRepo.UpdateAsync(booking, ct);
}
// null → no checkout; booking stays PendingPayment
```

Update `CreateBookingHandlerTests.cs`:
- Replace `IPaymentProviderFactory` mock with `ITenantPaymentProviderResolver` mock
- Replace `.GetProvider(...)` setup with `.ResolveAsync(...)` returns
- Add: `Handle_WhenResolverReturnsNull_SkipsCheckoutAndStaysAtPendingPayment`

Commit: `git commit -m "feat(application): swap IPaymentProviderFactory for ITenantPaymentProviderResolver in CreateBookingHandler"`

---

## Task 18: Functional tests

**Files:**
- Create: `tests/Chronith.Tests.Functional/TenantPaymentConfig/TenantPaymentConfigEndpointsTests.cs`
- Create: `tests/Chronith.Tests.Functional/TenantPaymentConfig/TenantPaymentConfigAuthTests.cs`

**Auth tests:** All 6 admin routes return 401 when anonymous, 403 when TenantStaff.
Public endpoint returns 200 when anonymous.

**Endpoint tests (happy paths):**
1. `POST /tenant/payment-config` → 201, returns `TenantPaymentConfigDto`
2. `GET /tenant/payment-config` → 200, list includes created config
3. `PUT /tenant/payment-config/{id}` → 200, label/settings updated
4. `PATCH /tenant/payment-config/{id}/activate` → 204
5. `PATCH /tenant/payment-config/{id}/deactivate` → 204
6. `DELETE /tenant/payment-config/{id}` → 204
7. `GET /public/test-tenant/payment-providers` → 200, returns active configs as summary

Run: `dotnet test tests/Chronith.Tests.Functional --filter "FullyQualifiedName~TenantPaymentConfig" 2>&1 | tail -10`

Full suite: `dotnet test Chronith.slnx 2>&1 | tail -15`

Commit: `git commit -m "test(functional): add TenantPaymentConfig endpoint and auth tests"`

---

## Task 19: Final verification + PR

```bash
dotnet build Chronith.slnx --no-incremental 2>&1 | tail -5
dotnet test Chronith.slnx 2>&1 | tail -15
```

Open PR targeting `main` with `feat/per-tenant-payment-config`.

---

## Settings JSON Reference

| Provider | Required fields |
|----------|----------------|
| `PayMongo` | `SecretKey`, `PublicKey`, `WebhookSecret`, `SuccessUrl`, `FailureUrl` |
| `Maya` | `PublicApiKey`, `SecretApiKey`, `SuccessUrl`, `FailureUrl` |
| `Manual` | _(none — use `{}`)_ |
| `Stub` | _(none — use `{}`)_ |
