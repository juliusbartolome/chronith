namespace Chronith.Domain.Exceptions;

public sealed class CustomFieldValidationException : DomainException
{
    public CustomFieldValidationException(string message) : base(message) { }
}
