namespace AminAmval.Application.Exceptions;

public sealed class ValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors) : base("اعتبارسنجی ناموفق بود.")
    {
        Errors = errors;
    }
}

public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}

public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}

public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }
}