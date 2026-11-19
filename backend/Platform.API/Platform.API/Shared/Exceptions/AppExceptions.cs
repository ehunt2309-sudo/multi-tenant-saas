namespace Platform.API.Shared.Exceptions;

/// <summary>Expected, user-facing errors. The global handler turns these into RFC 7807 responses.</summary>
public abstract class AppException : Exception
{
    protected AppException(string message, int status, string title) : base(message)
    {
        Status = status;
        Title = title;
    }

    public int Status { get; }
    public string Title { get; }
}

public sealed class BadRequestException(string message) : AppException(message, 400, "Bad request");
public sealed class UnauthorizedException(string message = "Authentication failed.") : AppException(message, 401, "Unauthorized");
public sealed class ForbiddenException(string message = "You do not have access to this resource.") : AppException(message, 403, "Forbidden");
public sealed class NotFoundException(string message = "Resource not found.") : AppException(message, 404, "Not found");
public sealed class ConflictException(string message) : AppException(message, 409, "Conflict");
