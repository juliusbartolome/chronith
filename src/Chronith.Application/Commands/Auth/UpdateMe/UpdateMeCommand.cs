using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using MediatR;

namespace Chronith.Application.Commands.Auth.UpdateMe;

public sealed record UpdateMeCommand(
    Guid UserId,
    string? Email,
    string? NewPassword) : IRequest<UserProfileDto>, IAuditable
{
    public Guid EntityId => UserId;
    public string EntityType => "TenantUser";
    public string Action => "Update";
}
