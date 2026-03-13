namespace Chronith.Application.DTOs;

public sealed record SignupResultDto(
    Guid TenantId,
    Guid AdminUserId,
    string Message
);
