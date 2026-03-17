namespace Chronith.Application.Interfaces;

public interface IAuditable
{
    Guid EntityId { get; }
    string EntityType { get; }
    string Action { get; }
}
