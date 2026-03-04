using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Exceptions;
using Chronith.Domain.Models;
using MediatR;

namespace Chronith.Application.Queries.Auth.GetMe;

public sealed class GetMeQueryHandler(ITenantUserRepository userRepository)
    : IRequestHandler<GetMeQuery, UserProfileDto>
{
    public async Task<UserProfileDto> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(TenantUser), request.UserId);

        return new UserProfileDto(user.Id, user.TenantId, user.Email, user.Role.ToString());
    }
}
