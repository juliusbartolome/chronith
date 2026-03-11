using Chronith.Application.Interfaces;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Services.Notifications;
using NSubstitute;
using FluentAssertions;

namespace Chronith.Tests.Unit.Infrastructure.Services;

public sealed class DefaultTemplateSeederTests
{
    private readonly INotificationTemplateRepository _templateRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DefaultTemplateSeeder _sut;

    private static readonly Guid TenantId = Guid.NewGuid();

    public DefaultTemplateSeederTests()
    {
        _templateRepo = Substitute.For<INotificationTemplateRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        // Default: no existing template found
        _templateRepo
            .GetByEventAndChannelAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(default(NotificationTemplate));

        _sut = new DefaultTemplateSeeder(_templateRepo, _unitOfWork);
    }

    [Fact]
    public async Task SeedForEventTypeAsync_SeedsThreeChannels_WhenNoneExist()
    {
        await _sut.SeedForEventTypeAsync(TenantId, "booking.confirmed");

        await _templateRepo.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<NotificationTemplate>>(templates =>
                templates.Count() == 3),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedForEventTypeAsync_SkipsExistingTemplates_WhenSomeAlreadyExist()
    {
        // Only email template already exists
        _templateRepo
            .GetByEventAndChannelAsync(TenantId, "booking.confirmed", "email", Arg.Any<CancellationToken>())
            .Returns(NotificationTemplate.Create(TenantId, "booking.confirmed", "email", "Subject", "Body"));

        await _sut.SeedForEventTypeAsync(TenantId, "booking.confirmed");

        // Only 2 templates created (sms and push)
        await _templateRepo.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<NotificationTemplate>>(templates =>
                templates.Count() == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedForEventTypeAsync_DoesNotCallAddRange_WhenAllTemplatesExist()
    {
        // All three channels exist
        _templateRepo
            .GetByEventAndChannelAsync(TenantId, "booking.confirmed", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(NotificationTemplate.Create(TenantId, "booking.confirmed", "email", "Subject", "Body"));

        await _sut.SeedForEventTypeAsync(TenantId, "booking.confirmed");

        await _templateRepo.DidNotReceive().AddRangeAsync(
            Arg.Any<IEnumerable<NotificationTemplate>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedAllAsync_Creates18Templates_WhenNoneExist()
    {
        var totalAdded = 0;
        _templateRepo
            .AddRangeAsync(Arg.Any<IEnumerable<NotificationTemplate>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                totalAdded += callInfo.Arg<IEnumerable<NotificationTemplate>>().Count();
                return Task.CompletedTask;
            });

        await _sut.SeedAllAsync(TenantId);

        // 6 event types x 3 channels = 18
        totalAdded.Should().Be(18);
    }

    [Fact]
    public async Task SeedAllAsync_CallsSaveChanges_ForEachEventType()
    {
        await _sut.SeedAllAsync(TenantId);

        // 6 event types
        await _unitOfWork.Received(6).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("booking.confirmed")]
    [InlineData("booking.cancelled")]
    [InlineData("booking.rescheduled")]
    [InlineData("waitlist.offered")]
    [InlineData("reminder")]
    [InlineData("customer.welcome")]
    public async Task SeedForEventTypeAsync_CreatesTemplates_WithCorrectEventType(string eventType)
    {
        await _sut.SeedForEventTypeAsync(TenantId, eventType);

        await _templateRepo.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<NotificationTemplate>>(templates =>
                templates.All(t => t.EventType == eventType)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedForEventTypeAsync_EmailTemplate_HasNonNullSubject()
    {
        NotificationTemplate? emailTemplate = null;
        _templateRepo
            .AddRangeAsync(Arg.Any<IEnumerable<NotificationTemplate>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                emailTemplate = callInfo.Arg<IEnumerable<NotificationTemplate>>()
                    .FirstOrDefault(t => t.ChannelType == "email");
                return Task.CompletedTask;
            });

        await _sut.SeedForEventTypeAsync(TenantId, "booking.confirmed");

        emailTemplate.Should().NotBeNull();
        emailTemplate!.Subject.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SeedForEventTypeAsync_AllTemplates_HaveNonEmptyBody()
    {
        IEnumerable<NotificationTemplate>? addedTemplates = null;
        _templateRepo
            .AddRangeAsync(Arg.Any<IEnumerable<NotificationTemplate>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                addedTemplates = callInfo.Arg<IEnumerable<NotificationTemplate>>();
                return Task.CompletedTask;
            });

        await _sut.SeedForEventTypeAsync(TenantId, "booking.confirmed");

        addedTemplates.Should().NotBeNull();
        addedTemplates!.Should().AllSatisfy(t => t.Body.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public async Task SeedForEventTypeAsync_AllTemplates_BelongToCorrectTenant()
    {
        IEnumerable<NotificationTemplate>? addedTemplates = null;
        _templateRepo
            .AddRangeAsync(Arg.Any<IEnumerable<NotificationTemplate>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                addedTemplates = callInfo.Arg<IEnumerable<NotificationTemplate>>();
                return Task.CompletedTask;
            });

        await _sut.SeedForEventTypeAsync(TenantId, "booking.confirmed");

        addedTemplates.Should().AllSatisfy(t => t.TenantId.Should().Be(TenantId));
    }
}
