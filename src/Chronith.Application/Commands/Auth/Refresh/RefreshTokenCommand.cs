using Chronith.Application.DTOs;
using MediatR;

namespace Chronith.Application.Commands.Auth.Refresh;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthTokenDto>;
