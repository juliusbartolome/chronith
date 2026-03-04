using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface IRefreshTokenRepository
{
    Task AddAsync(TenantUserRefreshToken token, CancellationToken ct = default);
    Task<TenantUserRefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    void Update(TenantUserRefreshToken token);
}
