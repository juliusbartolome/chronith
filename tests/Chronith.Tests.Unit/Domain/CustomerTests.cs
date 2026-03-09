using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Domain;

public sealed class CustomerTests
{
    [Fact]
    public void Create_BuiltIn_SetsAllProperties()
    {
        var tenantId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;

        var customer = Customer.Create(
            tenantId: tenantId,
            email: "alice@example.com",
            passwordHash: "hashed-password",
            name: "Alice",
            phone: "+639171234567",
            authProvider: "builtin");

        customer.Id.Should().NotBeEmpty();
        customer.TenantId.Should().Be(tenantId);
        customer.Email.Should().Be("alice@example.com");
        customer.PasswordHash.Should().Be("hashed-password");
        customer.Name.Should().Be("Alice");
        customer.Phone.Should().Be("+639171234567");
        customer.ExternalId.Should().BeNull();
        customer.AuthProvider.Should().Be("builtin");
        customer.IsEmailVerified.Should().BeFalse();
        customer.IsActive.Should().BeTrue();
        customer.IsDeleted.Should().BeFalse();
        customer.CreatedAt.Should().BeOnOrAfter(before);
        customer.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public void Create_Oidc_SetsExternalIdAndNullPassword()
    {
        var tenantId = Guid.NewGuid();

        var customer = Customer.CreateOidc(
            tenantId: tenantId,
            email: "bob@google.com",
            name: "Bob",
            externalId: "google-uid-123",
            authProvider: "oidc");

        customer.Id.Should().NotBeEmpty();
        customer.TenantId.Should().Be(tenantId);
        customer.Email.Should().Be("bob@google.com");
        customer.PasswordHash.Should().BeNull();
        customer.Name.Should().Be("Bob");
        customer.ExternalId.Should().Be("google-uid-123");
        customer.AuthProvider.Should().Be("oidc");
        customer.IsEmailVerified.Should().BeTrue();
        customer.IsActive.Should().BeTrue();
        customer.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void UpdateProfile_ChangesNameAndPhone()
    {
        var customer = Customer.Create(
            tenantId: Guid.NewGuid(),
            email: "test@example.com",
            passwordHash: "hash",
            name: "Old Name",
            phone: "+639170000000",
            authProvider: "builtin");

        customer.UpdateProfile("New Name", "+639171111111");

        customer.Name.Should().Be("New Name");
        customer.Phone.Should().Be("+639171111111");
    }

    [Fact]
    public void MarkEmailVerified_SetsFlag()
    {
        var customer = Customer.Create(
            tenantId: Guid.NewGuid(),
            email: "test@example.com",
            passwordHash: "hash",
            name: "Test",
            phone: null,
            authProvider: "builtin");

        customer.IsEmailVerified.Should().BeFalse();

        customer.MarkEmailVerified();

        customer.IsEmailVerified.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var customer = Customer.Create(
            tenantId: Guid.NewGuid(),
            email: "test@example.com",
            passwordHash: "hash",
            name: "Test",
            phone: null,
            authProvider: "builtin");

        customer.IsActive.Should().BeTrue();

        customer.Deactivate();

        customer.IsActive.Should().BeFalse();
    }

    [Fact]
    public void RecordLogin_UpdatesLastLoginAt()
    {
        var customer = Customer.Create(
            tenantId: Guid.NewGuid(),
            email: "test@example.com",
            passwordHash: "hash",
            name: "Test",
            phone: null,
            authProvider: "builtin");

        customer.LastLoginAt.Should().BeNull();

        var before = DateTimeOffset.UtcNow;
        customer.RecordLogin();

        customer.LastLoginAt.Should().NotBeNull();
        customer.LastLoginAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void SoftDelete_SetsIsDeletedTrue()
    {
        var customer = Customer.Create(
            tenantId: Guid.NewGuid(),
            email: "test@example.com",
            passwordHash: "hash",
            name: "Test",
            phone: null,
            authProvider: "builtin");

        customer.IsDeleted.Should().BeFalse();

        customer.SoftDelete();

        customer.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var customer = Customer.Create(
            tenantId: Guid.NewGuid(),
            email: "test@example.com",
            passwordHash: "hash",
            name: "Test",
            phone: null,
            authProvider: "builtin");

        customer.Deactivate();
        customer.IsActive.Should().BeFalse();

        customer.Activate();

        customer.IsActive.Should().BeTrue();
    }
}
