using Chronith.Application.Interfaces;
using Chronith.Application.Services;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Persistence.Entities;
using Chronith.Infrastructure.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chronith.Infrastructure.Persistence.Repositories;

public sealed class TenantUserRepository(
    ChronithDbContext context,
    IEncryptionService encryptionService,
    IBlindIndexService blindIndexService,
    ILogger<TenantUserRepository> logger)
    : ITenantUserRepository
{
    private string DecryptEmail(string? value)
    {
        if (value is null) return string.Empty;
        try { return encryptionService.Decrypt(value) ?? string.Empty; }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            logger.LogWarning("TenantUser.Email/EmailEncrypted could not be decrypted — " +
                "treating as legacy plaintext row. Next write will encrypt it.");
            return value;
        }
    }

    private TenantUser MapToDomain(TenantUserEntity e)
    {
        e.Email = e.EmailEncrypted is not null ? DecryptEmail(e.EmailEncrypted) : e.Email;
        return e.ToDomain();
    }

    public async Task AddAsync(TenantUser user, CancellationToken ct = default)
    {
        var entity = user.ToEntity();
        entity.EmailEncrypted = encryptionService.Encrypt(user.Email) ?? string.Empty;
        entity.EmailToken = blindIndexService.ComputeToken(user.Email);
        await context.TenantUsers.AddAsync(entity, ct);
    }

    public async Task<TenantUser?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await context.TenantUsers
            .TagWith("GetByIdAsync — TenantUserRepository")
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<TenantUser?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct = default)
    {
        var token = blindIndexService.ComputeToken(email);
        var normalised = email.ToLowerInvariant();
        var entity = await context.TenantUsers
            .TagWith("GetByEmailAsync — TenantUserRepository")
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId
                && (u.EmailToken == token || (u.EmailToken == null && u.Email == normalised)), ct);
        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<bool> ExistsByEmailAsync(Guid tenantId, string email, CancellationToken ct = default)
    {
        var token = blindIndexService.ComputeToken(email);
        var normalised = email.ToLowerInvariant();
        return await context.TenantUsers
            .TagWith("ExistsByEmailAsync — TenantUserRepository")
            .AnyAsync(u => u.TenantId == tenantId
                && (u.EmailToken == token || (u.EmailToken == null && u.Email == normalised)), ct);
    }

    public async Task<bool> ExistsByEmailGloballyAsync(string email, CancellationToken ct = default)
    {
        var token = blindIndexService.ComputeToken(email);
        var normalised = email.ToLowerInvariant();
        return await context.TenantUsers
            .TagWith("ExistsByEmailGloballyAsync — TenantUserRepository")
            .AnyAsync(u => u.EmailToken == token || (u.EmailToken == null && u.Email == normalised), ct);
    }

    public void Update(TenantUser user)
    {
        var entity = user.ToEntity();
        entity.EmailEncrypted = encryptionService.Encrypt(user.Email) ?? string.Empty;
        entity.EmailToken = blindIndexService.ComputeToken(user.Email);
        context.TenantUsers.Update(entity);
    }

    public Task UpdateAsync(TenantUser user, CancellationToken ct = default)
    {
        Update(user);
        return Task.CompletedTask;
    }
}
