using Chronith.Domain.Models;

namespace Chronith.Application.Interfaces;

public interface ICustomerRefreshTokenRepository
{
    Task AddAsync(CustomerRefreshToken token, CancellationToken ct = default);
    Task<CustomerRefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    void Update(CustomerRefreshToken token);
}
