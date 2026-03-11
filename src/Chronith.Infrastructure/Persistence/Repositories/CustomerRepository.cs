using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class CustomerRepository(ChronithDbContext db) : ICustomerRepository
{
    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Customers
            .TagWith("GetByIdAsync — CustomerRepository")
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        return entity?.ToDomain();
    }

    public async Task<Customer?> GetByIdCrossTenantAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Customers
            .TagWith("GetByIdCrossTenantAsync — CustomerRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        return entity?.ToDomain();
    }

    public Task<Customer?> GetByIdAcrossTenantsAsync(Guid customerId, CancellationToken ct = default)
        => GetByIdCrossTenantAsync(customerId, ct);

    public async Task<Customer?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct = default)
    {
        var entity = await db.Customers
            .TagWith("GetByEmailAsync — CustomerRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Email == email && !c.IsDeleted, ct);
        return entity?.ToDomain();
    }

    public async Task<Customer?> GetByExternalIdAsync(Guid tenantId, string externalId, CancellationToken ct = default)
    {
        var entity = await db.Customers
            .TagWith("GetByExternalIdAsync — CustomerRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ExternalId == externalId && !c.IsDeleted, ct);
        return entity?.ToDomain();
    }

    public async Task AddAsync(Customer customer, CancellationToken ct = default) =>
        await db.Customers.AddAsync(customer.ToEntity(), ct);

    public void Update(Customer customer) =>
        db.Customers.Update(customer.ToEntity());
}
