using Chronith.Application.DTOs;
using MediatR;

namespace Chronith.Application.Commands.Auth.UpdateMe;

public sealed record UpdateMeCommand(
    Guid UserId,
    string? Email,
    string? NewPassword) : IRequest<UserProfileDto>;
