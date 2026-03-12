using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Queries.Bookings;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Chronith.Tests.Unit.Application.Export;

public sealed class ExportBookingsQueryHandlerTests
{
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly IBookingRepository _bookingRepo = Substitute.For<IBookingRepository>();
    private readonly ICsvExportService _csvService = Substitute.For<ICsvExportService>();
    private readonly IPdfExportService _pdfService = Substitute.For<IPdfExportService>();

    private readonly ExportBookingsQueryHandler _sut;

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset From = DateTimeOffset.UtcNow.AddDays(-7);
    private static readonly DateTimeOffset To = DateTimeOffset.UtcNow;

    public ExportBookingsQueryHandlerTests()
    {
        _tenantContext.TenantId.Returns(TenantId);
        _sut = new ExportBookingsQueryHandler(
            _tenantContext, _bookingRepo, _csvService, _pdfService);
    }

    [Fact]
    public async Task Handle_CsvFormat_ReturnsCsvResult()
    {
        var rows = new List<BookingExportRowDto>
        {
            new(Guid.NewGuid(), "Haircut", "haircut",
                From, From.AddHours(1), "Confirmed",
                "customer@test.com", "cust-1", null,
                10_000, "PHP", null)
        };
        var expectedBytes = "id,bookingTypeName\r\n"u8.ToArray();

        _bookingRepo.ListForExportAsync(TenantId, From, To,
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(rows);
        _csvService.GenerateBookingsCsv(rows).Returns(expectedBytes);

        var result = await _sut.Handle(
            new ExportBookingsQuery(From, To, "csv"), CancellationToken.None);

        result.ContentType.Should().Be("text/csv");
        result.Content.Should().BeEquivalentTo(expectedBytes);
        result.FileName.Should().StartWith("bookings-").And.EndWith(".csv");
    }

    [Fact]
    public async Task Handle_PdfFormat_ReturnsPdfResult()
    {
        var rows = new List<BookingExportRowDto>();
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF

        _bookingRepo.ListForExportAsync(TenantId, From, To,
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(rows);
        _pdfService.GenerateBookingsPdf(rows, Arg.Any<string>(), From, To).Returns(pdfBytes);

        var result = await _sut.Handle(
            new ExportBookingsQuery(From, To, "pdf"), CancellationToken.None);

        result.ContentType.Should().Be("application/pdf");
        result.Content.Should().BeEquivalentTo(pdfBytes);
        result.FileName.Should().StartWith("bookings-").And.EndWith(".pdf");
    }

    [Fact]
    public async Task Handle_UnknownFormat_DefaultsToCsv()
    {
        _bookingRepo.ListForExportAsync(TenantId, From, To,
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new List<BookingExportRowDto>());
        _csvService.GenerateBookingsCsv(Arg.Any<IReadOnlyList<BookingExportRowDto>>())
            .Returns(Array.Empty<byte>());

        var result = await _sut.Handle(
            new ExportBookingsQuery(From, To, "xlsx"), CancellationToken.None);

        result.ContentType.Should().Be("text/csv");
    }
}
