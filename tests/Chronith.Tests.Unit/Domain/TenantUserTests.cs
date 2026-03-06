using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public class TenantUserTests
{
    [Fact]
    public void Create_NormalisesEmailToLowercase()
    {
        var user = TenantUser.Create(Guid.NewGuid(), "User@Example.COM", "hash", TenantUserRole.Owner);
        user.Email.Should().Be("user@example.com");
    }

    [Fact]
    public void Create_SetsIsActiveTrue()
    {
        var user = TenantUser.Create(Guid.NewGuid(), "a@b.com", "hash", TenantUserRole.Member);
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_SetsCreatedAtToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;
        var user = TenantUser.Create(Guid.NewGuid(), "a@b.com", "hash", TenantUserRole.Admin);
        user.CreatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void AuthorizationRole_ForOwner_IsTenantAdmin()
    {
        var user = TenantUser.Create(Guid.NewGuid(), "a@b.com", "hash", TenantUserRole.Owner);
        user.AuthorizationRole.Should().Be("TenantAdmin");
    }

    [Fact]
    public void AuthorizationRole_ForAdmin_IsTenantAdmin()
    {
        var user = TenantUser.Create(Guid.NewGuid(), "a@b.com", "hash", TenantUserRole.Admin);
        user.AuthorizationRole.Should().Be("TenantAdmin");
    }

    [Fact]
    public void AuthorizationRole_ForMember_IsTenantStaff()
    {
        var user = TenantUser.Create(Guid.NewGuid(), "a@b.com", "hash", TenantUserRole.Member);
        user.AuthorizationRole.Should().Be("TenantStaff");
    }
}
