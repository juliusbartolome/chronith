namespace Chronith.Application.DTOs;

public sealed record UserProfileDto(Guid UserId, Guid TenantId, string Email, string Role);
