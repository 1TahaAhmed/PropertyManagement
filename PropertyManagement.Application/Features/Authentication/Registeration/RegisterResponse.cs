namespace PropertyManagement.Application.Features.Authentication.Registeration;

public sealed record RegisterResponse(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    bool EmailConfirmed,
    DateTime CreatedAt);