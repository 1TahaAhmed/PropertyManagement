namespace PropertyManagement.Application.Abstractions.Authentication;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(
        Guid userId,
        string email,
        string firstName,
        string lastName,
        Guid? organizationId = null,
        string? role = null);

    (string PlainToken, string TokenHash, DateTime ExpiresAtUtc) GenerateRefreshToken();

    string HashToken(string token);
}
