using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Authentication;

namespace PropertyManagement.Infrastructure.Common.Security;

public sealed class JwtTokenGenerator(
    IOptions<JwtOptions> jwtOptions,
    IClock clock) : IJwtTokenGenerator
{
    private readonly JwtOptions _options = jwtOptions.Value;

    public string GenerateAccessToken(
        Guid userId,
        string email,
        string firstName,
        string lastName,
        Guid? organizationId = null,
        string? role = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.GivenName, firstName),
            new(JwtRegisteredClaimNames.FamilyName, lastName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (organizationId.HasValue)
        {
            claims.Add(new Claim("organization_id", organizationId.Value.ToString()));
            claims.Add(new Claim("tenant_id", organizationId.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expires = clock.UtcNow.AddMinutes(_options.ExpiryMinutes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(securityToken);
    }

    public (string PlainToken, string TokenHash, DateTime ExpiresAtUtc) GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        var plainToken = Convert.ToBase64String(randomBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var tokenHash = HashToken(plainToken);
        var expiresAtUtc = clock.UtcNow.AddDays(_options.RefreshTokenExpiryDays);

        return (plainToken, tokenHash, expiresAtUtc);
    }

    public string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
