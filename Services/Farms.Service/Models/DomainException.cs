namespace Farms.Service.Models;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}

public sealed class InvalidInputException : DomainException
{
    public InvalidInputException(string message) : base(message)
    {
    }
}

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message)
    {
    }
}

public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message) : base(message)
    {
    }
}

public sealed class UnauthenticatedException : DomainException
{
    public UnauthenticatedException() : base("Authentication is required.")
    {
    }
}

public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message)
    {
    }
}

public sealed class BusinessRuleException : DomainException
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}

public sealed class ConcurrencyException : DomainException
{
    public ConcurrencyException(string entityName)
        : base($"The {entityName} was modified by another request. Reload it and try again.")
    {
    }
}
