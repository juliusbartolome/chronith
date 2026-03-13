using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Chronith.Application.Commands.Signup;

// ── Command ──────────────────────────────────────────────────────────────────

/// <summary>
/// Public self-service signup: creates a tenant, owner user, and a trial subscription
/// on the free plan. Returns a lightweight result (no JWT — caller must login separately).
/// </summary>
public sealed record SignupCommand : IRequest<SignupResultDto>
{
    public required string TenantName { get; init; }
    public required string TenantSlug { get; init; }
    public required string TimeZoneId { get; init; }
    public required string Email { get; init; }
    public required string Password { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class SignupCommandValidator : AbstractValidator<SignupCommand>
{
    public SignupCommandValidator()
    {
        RuleFor(x => x.TenantName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TenantSlug).NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9-]+$").WithMessage("Slug may only contain lowercase letters, digits, and hyphens.");
        RuleFor(x => x.TimeZoneId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class SignupCommandHandler(
    ITenantRepository tenantRepository,
    ITenantUserRepository userRepository,
    ITenantSubscriptionRepository subscriptionRepository,
    IDefaultTemplateSeeder templateSeeder,
    IUnitOfWork unitOfWork,
    ILogger<SignupCommandHandler> logger
) : IRequestHandler<SignupCommand, SignupResultDto>
{
    // FreePlanId is seeded deterministically by PlanSeeder in Infrastructure.
    // Application layer cannot reference Infrastructure, so we use the fixed GUID directly.
    private static readonly Guid FreePlanId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public async Task<SignupResultDto> Handle(
        SignupCommand command, CancellationToken cancellationToken)
    {
        var slugExists = await tenantRepository.ExistsBySlugAsync(command.TenantSlug, cancellationToken);
        if (slugExists)
            throw new ConflictException($"Tenant slug '{command.TenantSlug}' is already taken.");

        // Create tenant
        var tenant = Tenant.Create(command.TenantSlug, command.TenantName, command.TimeZoneId);
        await tenantRepository.AddAsync(tenant, cancellationToken);

        // Hash password and create owner user
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(command.Password, workFactor: 12);
        var user = TenantUser.Create(tenant.Id, command.Email, passwordHash, TenantUserRole.Owner);
        await userRepository.AddAsync(user, cancellationToken);

        // Create free trial subscription
        var subscription = TenantSubscription.CreateTrial(tenant.Id, FreePlanId);
        await subscriptionRepository.AddAsync(subscription, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Seed default notification templates
        await templateSeeder.SeedAllAsync(tenant.Id, cancellationToken);

        // TODO: send welcome email to command.Email

        logger.LogInformation(
            "Signup completed: tenant {TenantId}, user {UserId}", tenant.Id, user.Id);

        return new SignupResultDto(tenant.Id, user.Id, "Account created successfully. Please log in.");
    }
}
