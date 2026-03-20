using Chronith.Infrastructure.Security;
using FluentAssertions;

namespace Chronith.Tests.Unit.Infrastructure.Security;

public sealed class Argon2idPasswordHasherTests
{
    private readonly Argon2idPasswordHasher _sut = new();

    [Fact]
    public void Hash_ReturnsArgon2idPhcString()
    {
        var hash = _sut.Hash("password123");
        hash.Should().StartWith("$argon2id");
    }

    [Fact]
    public void Hash_ProducesDifferentOutputsForSameInput()
    {
        var h1 = _sut.Hash("password123");
        var h2 = _sut.Hash("password123");
        h1.Should().NotBe(h2); // random salt per call
    }

    [Fact]
    public void Verify_ReturnsTrueForCorrectPassword()
    {
        var hash = _sut.Hash("correct-password");
        _sut.Verify("correct-password", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_ReturnsFalseForWrongPassword()
    {
        var hash = _sut.Hash("correct-password");
        _sut.Verify("wrong-password", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_ReturnsFalseForEmptyPassword()
    {
        var hash = _sut.Hash("correct-password");
        _sut.Verify("", hash).Should().BeFalse();
    }
}
