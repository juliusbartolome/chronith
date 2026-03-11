using Chronith.Application.Interfaces;
using Chronith.Application.Notifications;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Infrastructure.Services;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Chronith.Tests.Unit.Infrastructure.Services;

public sealed class RecurringBookingGeneratorServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid BookingTypeId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static RecurrenceRule BuildDailyRule(
        DateOnly? seriesEnd = null,
        TimeOnly? startTime = null,
        TimeSpan? duration = null)
        => RecurrenceRule.Create(
            tenantId: TenantId,
            bookingTypeId: BookingTypeId,
            customerId: CustomerId,
            staffMemberId: null,
            frequency: RecurrenceFrequency.Daily,
            interval: 1,
            daysOfWeek: null,
            startTime: startTime ?? new TimeOnly(9, 0),
            duration: duration ?? TimeSpan.FromHours(1),
            seriesStart: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            seriesEnd: seriesEnd,
            maxOccurrences: null);

    private static TimeSlotBookingType BuildBookingType(int capacity = 5, long priceInCentavos = 0)
    {
        var bt = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            tenantId: TenantId,
            priceInCentavos: priceInCentavos);
        // Override Id and Capacity via reflection (BookingTypeBuilder sets Capacity=1 by default)
        SetProperty(bt, "Id", BookingTypeId);
        SetProperty(bt, "Capacity", capacity);
        return bt;
    }

    private static void SetProperty(object obj, string name, object value)
    {
        var type = obj.GetType();
        while (type != null)
        {
            var field = type.GetField($"<{name}>k__BackingField",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) { field.SetValue(obj, value); return; }
            type = type.BaseType;
        }
        throw new InvalidOperationException($"Backing field for '{name}' not found.");
    }

    private static Tenant BuildTenant()
        => Tenant.Create("test-tenant", "Test Tenant", "UTC");

    private static Customer BuildCustomer()
        => Customer.Create(TenantId, "customer@example.com", null, "Test Customer", null, "local");

    private static (
        RecurringBookingGeneratorService Service,
        IRecurrenceRuleRepository RecurrenceRuleRepo,
        IBookingTypeRepository BookingTypeRepo,
        ITenantRepository TenantRepo,
        ICustomerRepository CustomerRepo,
        IBookingRepository BookingRepo,
        IUnitOfWork UnitOfWork,
        IPublisher Publisher)
        BuildSut(int horizonDays = 30)
    {
        var recurrenceRuleRepo = Substitute.For<IRecurrenceRuleRepository>();
        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        var tenantRepo = Substitute.For<ITenantRepository>();
        var customerRepo = Substitute.For<ICustomerRepository>();
        var bookingRepo = Substitute.For<IBookingRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var publisher = Substitute.For<IPublisher>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IRecurrenceRuleRepository)).Returns(recurrenceRuleRepo);
        serviceProvider.GetService(typeof(IBookingTypeRepository)).Returns(bookingTypeRepo);
        serviceProvider.GetService(typeof(ITenantRepository)).Returns(tenantRepo);
        serviceProvider.GetService(typeof(ICustomerRepository)).Returns(customerRepo);
        serviceProvider.GetService(typeof(IBookingRepository)).Returns(bookingRepo);
        serviceProvider.GetService(typeof(IUnitOfWork)).Returns(unitOfWork);
        serviceProvider.GetService(typeof(IPublisher)).Returns(publisher);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var options = Options.Create(new RecurringBookingGeneratorOptions
        {
            GenerationHorizonDays = horizonDays,
            CheckIntervalHours = 24
        });

        var logger = Substitute.For<ILogger<RecurringBookingGeneratorService>>();
        var healthTracker = Substitute.For<IBackgroundServiceHealthTracker>();

        var sut = new RecurringBookingGeneratorService(scopeFactory, options, healthTracker, logger);

        return (sut, recurrenceRuleRepo, bookingTypeRepo, tenantRepo, customerRepo, bookingRepo, unitOfWork, publisher);
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateBookingsAsync_CreatesBooking_ForEachOccurrenceWithNoConflict()
    {
        // Arrange
        var (sut, recurrenceRuleRepo, bookingTypeRepo, tenantRepo, customerRepo, bookingRepo, unitOfWork, publisher)
            = BuildSut(horizonDays: 3);

        var rule = BuildDailyRule();
        recurrenceRuleRepo.GetAllActiveAcrossTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<RecurrenceRule> { rule }.AsReadOnly());

        var bookingType = BuildBookingType(capacity: 5);
        bookingTypeRepo.GetByIdAcrossTenantsAsync(BookingTypeId, Arg.Any<CancellationToken>())
            .Returns(bookingType);

        var tenant = BuildTenant();
        tenantRepo.GetByIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var customer = BuildCustomer();
        customerRepo.GetByIdAcrossTenantsAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns(customer);

        // No conflicts (slot available)
        bookingRepo.CountConflictsAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<IReadOnlyList<BookingStatus>>(),
                Arg.Any<CancellationToken>())
            .Returns(0);

        // Act
        await sut.GenerateBookingsAsync(CancellationToken.None);

        // Assert: booking created for each occurrence (at least 1 occurrence in 3-day horizon)
        // Daily rule from yesterday + 3 day horizon = at least 1 occurrence
        await bookingRepo.ReceivedWithAnyArgs()
            .AddAsync(default!, default);

        await unitOfWork.ReceivedWithAnyArgs()
            .SaveChangesAsync(default);

        await publisher.ReceivedWithAnyArgs()
            .Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateBookingsAsync_SkipsOccurrence_WhenSlotIsFull()
    {
        // Arrange
        var (sut, recurrenceRuleRepo, bookingTypeRepo, tenantRepo, customerRepo, bookingRepo, unitOfWork, publisher)
            = BuildSut(horizonDays: 3);

        var rule = BuildDailyRule();
        recurrenceRuleRepo.GetAllActiveAcrossTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<RecurrenceRule> { rule }.AsReadOnly());

        var bookingType = BuildBookingType(capacity: 2);
        bookingTypeRepo.GetByIdAcrossTenantsAsync(BookingTypeId, Arg.Any<CancellationToken>())
            .Returns(bookingType);

        var tenant = BuildTenant();
        tenantRepo.GetByIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var customer = BuildCustomer();
        customerRepo.GetByIdAcrossTenantsAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns(customer);

        // Slot is full (conflicts >= capacity)
        bookingRepo.CountConflictsAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<IReadOnlyList<BookingStatus>>(),
                Arg.Any<CancellationToken>())
            .Returns(2);

        // Act
        await sut.GenerateBookingsAsync(CancellationToken.None);

        // Assert: no bookings created
        await bookingRepo.DidNotReceive()
            .AddAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());

        await publisher.DidNotReceive()
            .Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateBookingsAsync_SkipsRule_WhenBookingTypeNotFound()
    {
        // Arrange
        var (sut, recurrenceRuleRepo, bookingTypeRepo, tenantRepo, customerRepo, bookingRepo, unitOfWork, publisher)
            = BuildSut(horizonDays: 3);

        var rule = BuildDailyRule();
        recurrenceRuleRepo.GetAllActiveAcrossTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<RecurrenceRule> { rule }.AsReadOnly());

        // Booking type not found (returns null)
        bookingTypeRepo.GetByIdAcrossTenantsAsync(BookingTypeId, Arg.Any<CancellationToken>())
            .Returns((BookingType?)null);

        // Act
        var act = async () => await sut.GenerateBookingsAsync(CancellationToken.None);

        // Assert: no exception, no booking created
        await act.Should().NotThrowAsync();

        await bookingRepo.DidNotReceive()
            .AddAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateBookingsAsync_SkipsRule_WhenTenantNotFound()
    {
        // Arrange
        var (sut, recurrenceRuleRepo, bookingTypeRepo, tenantRepo, customerRepo, bookingRepo, unitOfWork, publisher)
            = BuildSut(horizonDays: 3);

        var rule = BuildDailyRule();
        recurrenceRuleRepo.GetAllActiveAcrossTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<RecurrenceRule> { rule }.AsReadOnly());

        var bookingType = BuildBookingType(capacity: 5);
        bookingTypeRepo.GetByIdAcrossTenantsAsync(BookingTypeId, Arg.Any<CancellationToken>())
            .Returns(bookingType);

        // Tenant not found
        tenantRepo.GetByIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        // Act
        var act = async () => await sut.GenerateBookingsAsync(CancellationToken.None);

        // Assert: no exception, no booking created
        await act.Should().NotThrowAsync();

        await bookingRepo.DidNotReceive()
            .AddAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateBookingsAsync_SkipsOccurrence_WhenCustomerNotFound()
    {
        // Arrange
        var (sut, recurrenceRuleRepo, bookingTypeRepo, tenantRepo, customerRepo, bookingRepo, unitOfWork, publisher)
            = BuildSut(horizonDays: 3);

        var rule = BuildDailyRule();
        recurrenceRuleRepo.GetAllActiveAcrossTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<RecurrenceRule> { rule }.AsReadOnly());

        var bookingType = BuildBookingType(capacity: 5);
        bookingTypeRepo.GetByIdAcrossTenantsAsync(BookingTypeId, Arg.Any<CancellationToken>())
            .Returns(bookingType);

        var tenant = BuildTenant();
        tenantRepo.GetByIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        // Customer not found
        customerRepo.GetByIdAcrossTenantsAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns((Customer?)null);

        // Act
        var act = async () => await sut.GenerateBookingsAsync(CancellationToken.None);

        // Assert: no exception, no booking created
        await act.Should().NotThrowAsync();

        await bookingRepo.DidNotReceive()
            .AddAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateBookingsAsync_DeactivatesRule_WhenSeriesEndHasPassed()
    {
        // Arrange
        var (sut, recurrenceRuleRepo, _, _, _, _, unitOfWork, _)
            = BuildSut(horizonDays: 3);

        // Series ended yesterday
        var pastSeriesEnd = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var rule = BuildDailyRule(seriesEnd: pastSeriesEnd);

        recurrenceRuleRepo.GetAllActiveAcrossTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<RecurrenceRule> { rule }.AsReadOnly());

        // Act
        await sut.GenerateBookingsAsync(CancellationToken.None);

        // Assert: rule's SoftDelete was called → recurrenceRuleRepo.Update was called with the deactivated rule
        recurrenceRuleRepo.Received(1).Update(Arg.Is<RecurrenceRule>(r => !r.IsActive && r.IsDeleted));

        // Assert: save was persisted
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateBookingsAsync_ContinuesProcessing_WhenOneRuleThrows()
    {
        // Arrange
        var (sut, recurrenceRuleRepo, bookingTypeRepo, tenantRepo, customerRepo, bookingRepo, unitOfWork, publisher)
            = BuildSut(horizonDays: 3);

        var throwingBookingTypeId = Guid.NewGuid();
        var workingBookingTypeId = Guid.NewGuid();

        // Rule 1: causes GetByIdAcrossTenantsAsync to throw (simulates transient failure)
        var rule1 = RecurrenceRule.Create(
            tenantId: TenantId,
            bookingTypeId: throwingBookingTypeId,
            customerId: CustomerId,
            staffMemberId: null,
            frequency: RecurrenceFrequency.Daily,
            interval: 1,
            daysOfWeek: null,
            startTime: new TimeOnly(9, 0),
            duration: TimeSpan.FromHours(1),
            seriesStart: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            seriesEnd: null,
            maxOccurrences: null);

        // Rule 2: works fine
        var rule2 = RecurrenceRule.Create(
            tenantId: TenantId,
            bookingTypeId: workingBookingTypeId,
            customerId: CustomerId,
            staffMemberId: null,
            frequency: RecurrenceFrequency.Daily,
            interval: 1,
            daysOfWeek: null,
            startTime: new TimeOnly(9, 0),
            duration: TimeSpan.FromHours(1),
            seriesStart: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            seriesEnd: null,
            maxOccurrences: null);

        recurrenceRuleRepo.GetAllActiveAcrossTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<RecurrenceRule> { rule1, rule2 }.AsReadOnly());

        // First rule's booking type lookup throws
        bookingTypeRepo.GetByIdAcrossTenantsAsync(throwingBookingTypeId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BookingType?>(new InvalidOperationException("Simulated transient failure")));

        // Second rule's booking type resolves successfully
        var workingBookingType = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            tenantId: TenantId,
            priceInCentavos: 0);
        SetProperty(workingBookingType, "Id", workingBookingTypeId);
        SetProperty(workingBookingType, "Capacity", 5);

        bookingTypeRepo.GetByIdAcrossTenantsAsync(workingBookingTypeId, Arg.Any<CancellationToken>())
            .Returns(workingBookingType);

        var tenant = BuildTenant();
        tenantRepo.GetByIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var customer = BuildCustomer();
        customerRepo.GetByIdAcrossTenantsAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns(customer);

        bookingRepo.CountConflictsAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<IReadOnlyList<BookingStatus>>(),
                Arg.Any<CancellationToken>())
            .Returns(0);

        // Act: must not propagate the exception from rule 1
        var act = async () => await sut.GenerateBookingsAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();

        // Assert: booking created at least once (for rule 2 — rule 1 threw before AddAsync)
        await bookingRepo.ReceivedWithAnyArgs()
            .AddAsync(default!, default);
    }

    [Fact]
    public async Task GenerateBookingsAsync_WithNoRules_DoesNothing()
    {
        // Arrange
        var (sut, recurrenceRuleRepo, _, _, _, bookingRepo, _, publisher) = BuildSut();

        recurrenceRuleRepo.GetAllActiveAcrossTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<RecurrenceRule>().AsReadOnly());

        // Act
        await sut.GenerateBookingsAsync(CancellationToken.None);

        // Assert
        await bookingRepo.DidNotReceive()
            .AddAsync(Arg.Any<Booking>(), Arg.Any<CancellationToken>());

        await publisher.DidNotReceive()
            .Publish(Arg.Any<BookingStatusChangedNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateBookingsAsync_CreatesBookingWithCorrectCustomerId()
    {
        // Arrange
        var (sut, recurrenceRuleRepo, bookingTypeRepo, tenantRepo, customerRepo, bookingRepo, unitOfWork, publisher)
            = BuildSut(horizonDays: 3);

        var rule = BuildDailyRule();
        recurrenceRuleRepo.GetAllActiveAcrossTenantsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<RecurrenceRule> { rule }.AsReadOnly());

        var bookingType = BuildBookingType(capacity: 5);
        bookingTypeRepo.GetByIdAcrossTenantsAsync(BookingTypeId, Arg.Any<CancellationToken>())
            .Returns(bookingType);

        var tenant = BuildTenant();
        tenantRepo.GetByIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var customer = BuildCustomer();
        customerRepo.GetByIdAcrossTenantsAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns(customer);

        bookingRepo.CountConflictsAsync(
                Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<IReadOnlyList<BookingStatus>>(),
                Arg.Any<CancellationToken>())
            .Returns(0);

        // Act
        await sut.GenerateBookingsAsync(CancellationToken.None);

        // Assert: booking created with CustomerId as string (Guid.ToString())
        await bookingRepo.Received()
            .AddAsync(
                Arg.Is<Booking>(b =>
                    b.CustomerId == CustomerId.ToString() &&
                    b.CustomerEmail == customer.Email &&
                    b.TenantId == TenantId &&
                    b.BookingTypeId == BookingTypeId),
                Arg.Any<CancellationToken>());
    }
}
