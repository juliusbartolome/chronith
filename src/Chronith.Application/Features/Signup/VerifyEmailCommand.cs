using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Chronith.Application.Features.Signup;

public sealed record VerifyEmailCommand : IRequest<Unit>
{
    public required string Token { get; init; }
}

public sealed class VerifyEmailCommandValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
    }
}

public sealed class VerifyEmailCommandHandler(
    ITenantUserRepository userRepo,
    ITokenService tokenService,
    IUnitOfWork unitOfWork
) : IRequestHandler<VerifyEmailCommand, Unit>
{
    public async Task<Unit> Handle(
        VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var userId = tokenService.ValidateEmailVerificationToken(request.Token)
            ?? throw new UnauthorizedException("Invalid or expired verification token.");

        var user = await userRepo.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        user.MarkEmailVerified();

        await userRepo.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
