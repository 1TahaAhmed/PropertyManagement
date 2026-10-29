namespace PropertyManagement.Application.Features.Authentication.Login;

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    Guid? OrganizationId,
    string? Role);
