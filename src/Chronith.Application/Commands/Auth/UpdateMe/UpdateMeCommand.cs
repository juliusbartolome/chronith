using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Services;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Commands.Auth.UpdateMe;

// ── Command ──────────────────────────────────────────────────────────────────

public sealed record UpdateMeCommand(
    Guid UserId,
    string? Email,
    string? NewPassword) : IRequest<UserProfileDto>, IAuditable
{
    public Guid EntityId => UserId;
    public string EntityType => "TenantUser";
    public string Action => "Update";
}

// ── Validator ────────────────────────────────────────────────────────────────

public sealed class UpdateMeCommandValidator : AbstractValidator<UpdateMeCommand>
{
    public UpdateMeCommandValidator()
    {
        When(x => x.Email is not null, () =>
            RuleFor(x => x.Email!).EmailAddress().MaximumLength(320));
        When(x => x.NewPassword is not null, () =>
            RuleFor(x => x.NewPassword!).MinimumLength(8)
                .Matches("[0-9]").WithMessage("Password must contain at least one digit."));
    }
}

// ── Handler ──────────────────────────────────────────────────────────────────

public sealed class UpdateMeCommandHandler(
    ITenantUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateMeCommand, UserProfileDto>
{
    public async Task<UserProfileDto> Handle(UpdateMeCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(TenantUser), request.UserId);

        if (request.Email is not null)
            user.UpdateEmail(request.Email);

        if (request.NewPassword is not null)
        {
            var hash = passwordHasher.Hash(request.NewPassword);
            user.UpdatePasswordHash(hash);
        }

        userRepository.Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserProfileDto(user.Id, user.TenantId, user.Email, user.Role.ToString());
    }
}
