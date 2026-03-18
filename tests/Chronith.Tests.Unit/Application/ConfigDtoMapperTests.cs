using Chronith.Application.DTOs;
using Chronith.Application.Mappers;
using Chronith.Domain.Models;
using FluentAssertions;

namespace Chronith.Tests.Unit.Application;

public sealed class TenantNotificationConfigMapperTests
{
    [Fact]
    public void ToDto_DoesNotExposeSettings()
    {
        // Settings contains sensitive credentials — must not be included in the API response DTO.
        var hasProp = typeof(TenantNotificationConfigDto)
            .GetProperty("Settings") is not null;

        hasProp.Should().BeFalse(
            "TenantNotificationConfigDto must not expose Settings because it contains sensitive credentials");
    }
}

public sealed class TenantPaymentConfigMapperTests
{
    [Fact]
    public void ToDto_DoesNotExposeSettings()
    {
        // Settings contains sensitive credentials — must not be included in the API response DTO.
        var hasProp = typeof(TenantPaymentConfigDto)
            .GetProperty("Settings") is not null;

        hasProp.Should().BeFalse(
            "TenantPaymentConfigDto must not expose Settings because it contains sensitive credentials");
    }
}
