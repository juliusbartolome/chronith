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
            firstName: "Alice",
            lastName: "Bartolome",
            mobile: "+639171234567",
            authProvider: "builtin");

        customer.Id.Should().NotBeEmpty();
        customer.TenantId.Should().Be(tenantId);
        customer.Email.Should().Be("alice@example.com");
        customer.PasswordHash.Should().Be("hashed-password");
        customer.FirstName.Should().Be("Alice");
        customer.LastName.Should().Be("Bartolome");
        customer.Mobile.Should().Be("+639171234567");
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
        customer.FirstName.Should().Be("Bob");
        customer.LastName.Should().Be(string.Empty);
        customer.ExternalId.Should().Be("google-uid-123");
        customer.AuthProvider.Should().Be("oidc");
        customer.IsEmailVerified.Should().BeTrue();
        customer.IsActive.Should().BeTrue();
        customer.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void CreateOidc_SplitsNameOnFirstSpace()
    {
        var customer = Customer.CreateOidc(
            Guid.NewGuid(), "test@example.com", "Julius Bartolome",
            "ext-123", "google");

        customer.FirstName.Should().Be("Julius");
        customer.LastName.Should().Be("Bartolome");
    }

    [Fact]
    public void CreateOidc_SingleName_PutsAllInFirstName()
    {
        var customer = Customer.CreateOidc(
            Guid.NewGuid(), "test@example.com", "Madonna",
            "ext-456", "google");

        customer.FirstName.Should().Be("Madonna");
        customer.LastName.Should().Be(string.Empty);
    }

    [Fact]
    public void CreateOidc_MultipleSpaces_SplitsOnFirstSpaceOnly()
    {
        var customer = Customer.CreateOidc(
            Guid.NewGuid(), "test@example.com", "Julius De La Cruz",
            "ext-789", "google");

        customer.FirstName.Should().Be("Julius");
        customer.LastName.Should().Be("De La Cruz");
    }

    [Fact]
    public void UpdateProfile_SetsFirstNameLastNameMobile()
    {
        var customer = Customer.Create(
            tenantId: Guid.NewGuid(),
            email: "test@example.com",
            passwordHash: "hash",
            firstName: "Old",
            lastName: "Name",
            mobile: "+639170000000",
            authProvider: "builtin");

        customer.UpdateProfile("New", "Last", "+639171111111");

        customer.FirstName.Should().Be("New");
        customer.LastName.Should().Be("Last");
        customer.Mobile.Should().Be("+639171111111");
    }

    [Fact]
    public void Hydrate_SetsAllFields()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        var customer = Customer.Hydrate(
            id, tenantId, "test@example.com", null,
            "Julius", "Bartolome", "+639171234567",
            null, "builtin", true, true, false,
            createdAt, null, 0);

        customer.Id.Should().Be(id);
        customer.TenantId.Should().Be(tenantId);
        customer.Email.Should().Be("test@example.com");
        customer.FirstName.Should().Be("Julius");
        customer.LastName.Should().Be("Bartolome");
        customer.Mobile.Should().Be("+639171234567");
        customer.AuthProvider.Should().Be("builtin");
        customer.IsEmailVerified.Should().BeTrue();
        customer.IsActive.Should().BeTrue();
        customer.IsDeleted.Should().BeFalse();
        customer.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void MarkEmailVerified_SetsFlag()
    {
        var customer = Customer.Create(
            tenantId: Guid.NewGuid(),
            email: "test@example.com",
            passwordHash: "hash",
            firstName: "Test",
            lastName: "User",
            mobile: null,
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
            firstName: "Test",
            lastName: "User",
            mobile: null,
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
            firstName: "Test",
            lastName: "User",
            mobile: null,
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
            firstName: "Test",
            lastName: "User",
            mobile: null,
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
            firstName: "Test",
            lastName: "User",
            mobile: null,
            authProvider: "builtin");

        customer.Deactivate();
        customer.IsActive.Should().BeFalse();

        customer.Activate();

        customer.IsActive.Should().BeTrue();
    }
}
