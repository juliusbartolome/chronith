using Chronith.Application.DTOs;
using MediatR;

namespace Chronith.Application.Queries.Auth.GetMe;

public sealed record GetMeQuery(Guid UserId) : IRequest<UserProfileDto>;
