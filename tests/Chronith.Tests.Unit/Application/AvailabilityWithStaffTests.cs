using Chronith.Application.DTOs;
using Chronith.Application.Interfaces;
using Chronith.Application.Queries.Availability;
using Chronith.Domain.Enums;
using Chronith.Domain.Models;
using Chronith.Tests.Unit.Helpers;
using FluentAssertions;
using NSubstitute;

namespace Chronith.Tests.Unit.Application;

public sealed class AvailabilityWithStaffTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    // Monday 2026-04-06 00:00 UTC to Tuesday 2026-04-07 00:00 UTC
    private static readonly DateTimeOffset From = new(2026, 4, 6, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 4, 7, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// When RequiresStaffAssignment is true and no staff are assigned to the
    /// booking type, every generated slot should be filtered out.
    /// </summary>
    [Fact]
    public async Task Handle_RequiresStaff_NoStaffAssigned_ReturnsEmpty()
    {
        // Arrange
        var bookingType = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            windows: [new TimeSlotWindow(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0))],
            tenantId: TenantId,
            requiresStaffAssignment: true);

        var (handler, staffRepo) = CreateHandler(bookingType);

        // No staff assigned to this booking type
        staffRepo.ListByBookingTypeAsync(TenantId, bookingType.Id, Arg.Any<CancellationToken>())
            .Returns(new List<StaffMember>());

        var query = new GetAvailabilityQuery
        {
            BookingTypeSlug = "test-slot",
            From = From,
            To = To
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert — no staff means no available slots
        result.Slots.Should().BeEmpty();
    }

    /// <summary>
    /// When RequiresStaffAssignment is true and one staff member covers only
    /// part of the booking type's availability window, only the covered slots
    /// should be returned.
    /// </summary>
    [Fact]
    public async Task Handle_RequiresStaff_StaffCoversPartialWindow_ReturnsOnlyCoveredSlots()
    {
        // Arrange — booking type available 09:00-12:00 Monday (3 × 60-min slots)
        var bookingType = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            windows: [new TimeSlotWindow(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0))],
            tenantId: TenantId,
            requiresStaffAssignment: true);

        // Staff available 10:00-12:00 Monday only — should cover 10:00 and 11:00 slots
        var staff = StaffMemberBuilder.Build(
            tenantId: TenantId,
            windows: [new StaffAvailabilityWindow(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(12, 0))]);

        var (handler, staffRepo) = CreateHandler(bookingType);

        staffRepo.ListByBookingTypeAsync(TenantId, bookingType.Id, Arg.Any<CancellationToken>())
            .Returns(new List<StaffMember> { staff });

        var query = new GetAvailabilityQuery
        {
            BookingTypeSlug = "test-slot",
            From = From,
            To = To
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert — only 10:00 and 11:00 slots, NOT the 09:00 slot
        result.Slots.Should().HaveCount(2);
        result.Slots[0].Start.Should().Be(new DateTimeOffset(2026, 4, 6, 10, 0, 0, TimeSpan.Zero));
        result.Slots[1].Start.Should().Be(new DateTimeOffset(2026, 4, 6, 11, 0, 0, TimeSpan.Zero));
    }

    /// <summary>
    /// When RequiresStaffAssignment is false, staff availability should NOT
    /// be checked — all generated slots are returned.
    /// </summary>
    [Fact]
    public async Task Handle_DoesNotRequireStaff_ReturnsAllSlots()
    {
        // Arrange — booking type available 09:00-12:00 Monday (3 × 60-min slots)
        var bookingType = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            windows: [new TimeSlotWindow(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0))],
            tenantId: TenantId);
        // RequiresStaffAssignment defaults to false

        var (handler, _) = CreateHandler(bookingType);

        var query = new GetAvailabilityQuery
        {
            BookingTypeSlug = "test-slot",
            From = From,
            To = To
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert — all 3 slots returned (09, 10, 11)
        result.Slots.Should().HaveCount(3);
    }

    /// <summary>
    /// When multiple staff members are assigned and their windows collectively
    /// cover all booking type slots, all slots should be returned.
    /// </summary>
    [Fact]
    public async Task Handle_RequiresStaff_MultipleStaffCoverAllSlots_ReturnsAll()
    {
        // Arrange — booking type available 09:00-12:00 Monday
        var bookingType = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            windows: [new TimeSlotWindow(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0))],
            tenantId: TenantId,
            requiresStaffAssignment: true);

        // Staff A covers 09:00-10:00, Staff B covers 10:00-12:00
        var staffA = StaffMemberBuilder.Build(
            tenantId: TenantId,
            windows: [new StaffAvailabilityWindow(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(10, 0))]);
        var staffB = StaffMemberBuilder.Build(
            tenantId: TenantId,
            windows: [new StaffAvailabilityWindow(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(12, 0))]);

        var (handler, staffRepo) = CreateHandler(bookingType);

        staffRepo.ListByBookingTypeAsync(TenantId, bookingType.Id, Arg.Any<CancellationToken>())
            .Returns(new List<StaffMember> { staffA, staffB });

        var query = new GetAvailabilityQuery
        {
            BookingTypeSlug = "test-slot",
            From = From,
            To = To
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert — all 3 slots: staffA covers 09:00, staffB covers 10:00+11:00
        result.Slots.Should().HaveCount(3);
    }

    /// <summary>
    /// Inactive staff members should be excluded from availability consideration.
    /// </summary>
    [Fact]
    public async Task Handle_RequiresStaff_InactiveStaffIgnored_ReturnsEmpty()
    {
        var bookingType = BookingTypeBuilder.BuildTimeSlot(
            durationMinutes: 60,
            windows: [new TimeSlotWindow(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0))],
            tenantId: TenantId,
            requiresStaffAssignment: true);

        // Staff covers the window but is deactivated
        var staff = StaffMemberBuilder.Build(
            tenantId: TenantId,
            windows: [new StaffAvailabilityWindow(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0))]);
        staff.Deactivate();

        var (handler, staffRepo) = CreateHandler(bookingType);

        staffRepo.ListByBookingTypeAsync(TenantId, bookingType.Id, Arg.Any<CancellationToken>())
            .Returns(new List<StaffMember> { staff });

        var query = new GetAvailabilityQuery
        {
            BookingTypeSlug = "test-slot",
            From = From,
            To = To
        };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert — inactive staff doesn't count
        result.Slots.Should().BeEmpty();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private (GetAvailabilityHandler handler, IStaffMemberRepository staffRepo) CreateHandler(
        BookingType bookingType)
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId);

        var bookingTypeRepo = Substitute.For<IBookingTypeRepository>();
        bookingTypeRepo.GetBySlugAsync(TenantId, "test-slot", Arg.Any<CancellationToken>())
            .Returns(bookingType);

        var bookingRepo = Substitute.For<IBookingRepository>();
        bookingRepo.GetBookedSlotsAsync(
                bookingType.Id, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<IReadOnlyList<BookingStatus>>(), Arg.Any<CancellationToken>())
            .Returns(new List<(DateTimeOffset, DateTimeOffset)>());

        var tenant = Tenant.Create("test-tenant", "Test Tenant", "UTC");
        var tenantRepo = Substitute.For<ITenantRepository>();
        tenantRepo.GetByIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(tenant);

        var timeBlockRepo = Substitute.For<ITimeBlockRepository>();
        timeBlockRepo.ListInRangeAsync(
                TenantId, Arg.Any<Guid?>(), Arg.Any<Guid?>(),
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new List<TimeBlock>());

        var slotGenerator = Substitute.For<ISlotGeneratorService>();
        slotGenerator.GenerateAvailableSlots(
                bookingType, Arg.Any<TenantTimeZone>(),
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<IReadOnlyList<(DateTimeOffset, DateTimeOffset)>>())
            .Returns(callInfo =>
            {
                // Generate real slots for 60-min duration within availability windows
                var bt = callInfo.ArgAt<BookingType>(0);
                var from = callInfo.ArgAt<DateTimeOffset>(2);
                var to = callInfo.ArgAt<DateTimeOffset>(3);
                var booked = callInfo.ArgAt<IReadOnlyList<(DateTimeOffset, DateTimeOffset)>>(4);

                if (bt is TimeSlotBookingType tsbt)
                {
                    var slots = new List<(DateTimeOffset, DateTimeOffset)>();
                    var tz = new TenantTimeZone("UTC");
                    var startDate = tz.ToLocalDate(from);
                    var endDate = tz.ToLocalDate(to);
                    var currentDate = startDate;

                    while (currentDate <= endDate)
                    {
                        foreach (var window in tsbt.AvailabilityWindows
                            .Where(w => w.DayOfWeek == currentDate.DayOfWeek))
                        {
                            var cursor = window.StartTime;
                            while (cursor.AddMinutes(tsbt.DurationMinutes) <= window.EndTime)
                            {
                                var slotStart = tz.ToUtc(currentDate, cursor);
                                var slotEnd = slotStart.AddMinutes(tsbt.DurationMinutes);

                                if (slotEnd > from && slotStart < to)
                                {
                                    var isBooked = booked.Any(b => slotStart < b.Item2 && slotEnd > b.Item1);
                                    if (!isBooked)
                                        slots.Add((slotStart, slotEnd));
                                }
                                cursor = cursor.AddMinutes(tsbt.DurationMinutes);
                            }
                        }
                        currentDate = currentDate.AddDays(1);
                    }
                    return (IReadOnlyList<(DateTimeOffset, DateTimeOffset)>)slots;
                }
                return (IReadOnlyList<(DateTimeOffset, DateTimeOffset)>)new List<(DateTimeOffset, DateTimeOffset)>();
            });

        var staffRepo = Substitute.For<IStaffMemberRepository>();

        var handler = new GetAvailabilityHandler(
            tenantContext, bookingTypeRepo, bookingRepo, tenantRepo, timeBlockRepo, slotGenerator,
            staffRepo: staffRepo);

        return (handler, staffRepo);
    }
}
