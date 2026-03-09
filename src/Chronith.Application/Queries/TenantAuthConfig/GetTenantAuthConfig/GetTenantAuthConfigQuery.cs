using Chronith.Application.Behaviors;
using Chronith.Application.DTOs;
using MediatR;

namespace Chronith.Application.Queries.TenantAuthConfig.GetTenantAuthConfig;

public sealed record GetTenantAuthConfigQuery : IRequest<TenantAuthConfigDto?>, IQuery;
