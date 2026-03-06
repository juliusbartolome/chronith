using BCrypt.Net;
using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using MediatR;

namespace Chronith.Application.Commands.Auth.UpdateMe;

public sealed class UpdateMeCommandHandler(
    ITenantUserRepository userRepository,
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
            var hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
            user.UpdatePasswordHash(hash);
        }

        userRepository.Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserProfileDto(user.Id, user.TenantId, user.Email, user.Role.ToString());
    }
}
