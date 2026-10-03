namespace PropertyManagement.Application.Features.Authentication.Registeration;

public sealed record RegisterResponse(
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    bool EmailConfirmed,
    DateTime CreatedAt);