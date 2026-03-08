using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronith.Tests.Unit.Infrastructure;

public class WebhookDispatcherCustomerCallbackTests
{
    private readonly IWebhookOutboxRepository _outboxRepo = Substitute.For<IWebhookOutboxRepository>();
    private readonly IWebhookRepository _webhookRepo = Substitute.For<IWebhookRepository>();
    private readonly IBookingTypeRepository _bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();

    private WebhookDispatcherService CreateSut()
    {
        var scope = Substitute.For<IServiceScope>();
        var sp = Substitute.For<IServiceProvider>();
        sp.GetService(typeof(IWebhookOutboxRepository)).Returns(_outboxRepo);
        sp.GetService(typeof(IWebhookRepository)).Returns(_webhookRepo);
        sp.GetService(typeof(IBookingTypeRepository)).Returns(_bookingTypeRepo);
        scope.ServiceProvider.Returns(sp);
        _scopeFactory.CreateScope().Returns(scope);

        var opts = Options.Create(new WebhookDispatcherOptions { DispatchIntervalSeconds = 10 });
        return new WebhookDispatcherService(
            _scopeFactory, _httpClientFactory, opts, NullLogger<WebhookDispatcherService>.Instance);
    }

    [Fact]
    public async Task DispatchBatch_CustomerCallbackEntry_WhenCallbackUrlNull_MarksAbandoned()
    {
        var bookingTypeId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        _outboxRepo.GetPendingAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new PendingOutboxEntry(entryId, Guid.NewGuid(), null, bookingTypeId,
                "customer.booking.confirmed", "{}", 0, OutboxCategory.CustomerCallback)]);

        // BookingType has no callback URL
        var bt = (BookingType)typeof(TimeSlotBookingType)
            .GetConstructor(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, [], null)!
            .Invoke([]);
        _bookingTypeRepo.GetByIdAsync(bookingTypeId, Arg.Any<CancellationToken>()).Returns(bt);

        var sut = CreateSut();
        await sut.DispatchBatchAsync(CancellationToken.None);

        await _outboxRepo.Received(1).MarkAbandonedAsync(entryId, Arg.Any<CancellationToken>());
    }
}
