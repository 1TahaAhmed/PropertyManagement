namespace PropertyManagement.Application.Abstractions.Identity;

public sealed record AuthUserResult(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName);
