using Chronith.Application.Mappers;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;

namespace Chronith.Tests.Unit.Application;

public sealed class BookingMapperTests
{
    [Fact]
    public void ToDto_SetsReferenceId_ToDashlessGuid()
    {
        // Arrange
        var booking = new BookingBuilder().Build();
        var expected = booking.Id.ToString("N");

        // Act
        var dto = booking.ToDto();

        // Assert
        dto.ReferenceId.Should().Be(expected);
        dto.ReferenceId.Should().NotContain("-");
    }
}
