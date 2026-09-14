namespace Assessment_Agit.Domain.Exceptions;

public class DomainValidationException : Exception
{
    public DomainValidationException(string message) : base(message) { }
}

public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}

public class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key) 
        : base($"Entity '{entityName}' with key '{key}' was not found.") { }
    
    public NotFoundException(string message) : base(message) { }
}

public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message) : base(message) { }
}

