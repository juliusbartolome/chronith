namespace Chronith.Domain.Exceptions;

public sealed class SlugAlreadyExistsException : DomainException
{
    public SlugAlreadyExistsException(string slug)
        : base($"Slug '{slug}' already exists for this tenant.") { }
}
