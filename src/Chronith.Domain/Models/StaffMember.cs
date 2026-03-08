namespace Chronith.Domain.Models;

public sealed class StaffMember
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? TenantUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<StaffAvailabilityWindow> _availabilityWindows = [];
    public IReadOnlyList<StaffAvailabilityWindow> AvailabilityWindows => _availabilityWindows.AsReadOnly();

    internal StaffMember() { }

    public static StaffMember Create(
        Guid tenantId,
        Guid? tenantUserId,
        string name,
        string email,
        IReadOnlyList<StaffAvailabilityWindow> availabilityWindows)
    {
        var staff = new StaffMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenantUserId = tenantUserId,
            Name = name,
            Email = email,
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        staff._availabilityWindows.AddRange(availabilityWindows);
        return staff;
    }

    public void Update(
        string name,
        string email,
        IReadOnlyList<StaffAvailabilityWindow> availabilityWindows)
    {
        Name = name;
        Email = email;
        _availabilityWindows.Clear();
        _availabilityWindows.AddRange(availabilityWindows);
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
    public void SoftDelete() => IsDeleted = true;
}
