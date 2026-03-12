using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Mappers;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.TenantSettings;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record UpdateTenantSettingsCommand : IRequest<TenantSettingsDto>
{
    public string? LogoUrl { get; init; }
    public string? PrimaryColor { get; init; }
    public string? AccentColor { get; init; }
    public string? CustomDomain { get; init; }
    public bool? BookingPageEnabled { get; init; }
    public string? WelcomeMessage { get; init; }
    public string? TermsUrl { get; init; }
    public string? PrivacyUrl { get; init; }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class UpdateTenantSettingsCommandValidator : AbstractValidator<UpdateTenantSettingsCommand>
{
    private static readonly System.Text.RegularExpressions.Regex HexColorRegex =
        new(@"^#[0-9A-Fa-f]{6}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public UpdateTenantSettingsCommandValidator()
    {
        When(x => x.PrimaryColor is not null, () =>
            RuleFor(x => x.PrimaryColor!)
                .Must(c => HexColorRegex.IsMatch(c))
                .WithMessage("PrimaryColor must be a valid hex color (e.g. #2563EB)."));

        When(x => x.AccentColor is not null, () =>
            RuleFor(x => x.AccentColor!)
                .Must(c => HexColorRegex.IsMatch(c))
                .WithMessage("AccentColor must be a valid hex color (e.g. #2563EB)."));

        When(x => x.LogoUrl is not null, () =>
            RuleFor(x => x.LogoUrl!).MaximumLength(2048));

        When(x => x.WelcomeMessage is not null, () =>
            RuleFor(x => x.WelcomeMessage!).MaximumLength(500));
    }
}

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class UpdateTenantSettingsCommandHandler(
    ITenantSettingsRepository settingsRepo,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateTenantSettingsCommand, TenantSettingsDto>
{
    public async Task<TenantSettingsDto> Handle(UpdateTenantSettingsCommand cmd, CancellationToken ct)
    {
        var settings = await settingsRepo.GetOrCreateAsync(tenantContext.TenantId, ct);

        settings.UpdateBranding(
            logoUrl: cmd.LogoUrl,
            primaryColor: cmd.PrimaryColor,
            accentColor: cmd.AccentColor,
            welcomeMessage: cmd.WelcomeMessage,
            termsUrl: cmd.TermsUrl,
            privacyUrl: cmd.PrivacyUrl);

        if (cmd.CustomDomain is not null)
            settings.SetCustomDomain(cmd.CustomDomain);

        if (cmd.BookingPageEnabled.HasValue)
        {
            if (cmd.BookingPageEnabled.Value)
                settings.EnableBookingPage();
            else
                settings.DisableBookingPage();
        }

        await settingsRepo.UpdateAsync(settings, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return settings.ToDto();
    }
}
