using Chronith.Application.Interfaces;
using Chronith.Application.Services;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class CustomerRepository(
    ChronithDbContext db,
    IEncryptionService encryptionService,
    IBlindIndexService blindIndexService,
    ILogger<CustomerRepository> logger)
    : ICustomerRepository
{
    private string DecryptEmail(string? value)
    {
        if (value is null) return string.Empty;
        try { return encryptionService.Decrypt(value) ?? string.Empty; }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            logger.LogWarning("Customer.Email/EmailEncrypted could not be decrypted — " +
                "treating as legacy plaintext row. Next write will encrypt it.");
            return value;
        }
    }

    private string? DecryptMobile(string? value)
    {
        if (value is null) return null;
        try { return encryptionService.Decrypt(value); }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            logger.LogWarning("Customer.MobileEncrypted could not be decrypted — " +
                "treating as legacy plaintext row. Next write will encrypt it.");
            return value;
        }
    }

    private Customer MapToDomain(Entities.CustomerEntity e)
    {
        // Prefer encrypted values if available; fall back to plaintext Email column
        e.Email = e.EmailEncrypted is not null ? DecryptEmail(e.EmailEncrypted) : e.Email;
        if (e.MobileEncrypted is not null)
            e.Mobile = DecryptMobile(e.MobileEncrypted);
        return e.ToDomain();
    }

    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Customers
            .TagWith("GetByIdAsync — CustomerRepository")
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<Customer?> GetByIdCrossTenantAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Customers
            .TagWith("GetByIdCrossTenantAsync — CustomerRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);
        return entity is null ? null : MapToDomain(entity);
    }

    public Task<Customer?> GetByIdAcrossTenantsAsync(Guid customerId, CancellationToken ct = default)
        => GetByIdCrossTenantAsync(customerId, ct);

    public async Task<Customer?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct = default)
    {
        var token = blindIndexService.ComputeToken(email);
        var entity = await db.Customers
            .TagWith("GetByEmailAsync — CustomerRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && !c.IsDeleted
                && (c.EmailToken == token || (c.EmailToken == null && c.Email == email)), ct);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<Customer?> GetByExternalIdAsync(Guid tenantId, string externalId, CancellationToken ct = default)
    {
        var entity = await db.Customers
            .TagWith("GetByExternalIdAsync — CustomerRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ExternalId == externalId && !c.IsDeleted, ct);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task AddAsync(Customer customer, CancellationToken ct = default)
    {
        var entity = customer.ToEntity();
        entity.EmailEncrypted = encryptionService.Encrypt(customer.Email) ?? string.Empty;
        entity.EmailToken = blindIndexService.ComputeToken(customer.Email);
        if (customer.Mobile is not null)
            entity.MobileEncrypted = encryptionService.Encrypt(customer.Mobile);
        await db.Customers.AddAsync(entity, ct);
    }

    public void Update(Customer customer)
    {
        var entity = customer.ToEntity();
        entity.EmailEncrypted = encryptionService.Encrypt(customer.Email) ?? string.Empty;
        entity.EmailToken = blindIndexService.ComputeToken(customer.Email);
        entity.MobileEncrypted = customer.Mobile is not null ? encryptionService.Encrypt(customer.Mobile) : null;
        db.Customers.Update(entity);
    }

    public Task<int> CountByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        db.Customers
            .TagWith("CountByTenantAsync — CustomerRepository")
            .AsNoTracking()
            .IgnoreQueryFilters()
            .CountAsync(c => c.TenantId == tenantId && !c.IsDeleted, ct);
}
