using Chronith.Domain.Enums;

namespace Chronith.Application.DTOs;

public sealed record BookingMetrics(
    int Total,
    Dictionary<BookingStatus, int> ByStatus,
    int ThisMonth);
