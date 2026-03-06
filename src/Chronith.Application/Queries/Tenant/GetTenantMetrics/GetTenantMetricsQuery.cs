using Chronith.Application.DTOs;
using MediatR;

namespace Chronith.Application.Queries.Tenant.GetTenantMetrics;

public sealed record GetTenantMetricsQuery : IRequest<TenantMetricsDto>;
