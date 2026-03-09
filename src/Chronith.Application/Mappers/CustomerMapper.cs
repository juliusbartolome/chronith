using Chronith.Application.DTOs;
using Chronith.Domain.Models;

namespace Chronith.Application.Mappers;

public static class CustomerMapper
{
    public static CustomerDto ToDto(this Customer customer) =>
        new(customer.Id, customer.Email, customer.Name, customer.Phone,
            customer.AuthProvider, customer.IsEmailVerified, customer.CreatedAt);
}
