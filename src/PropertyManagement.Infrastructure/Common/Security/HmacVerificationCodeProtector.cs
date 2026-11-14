using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Infrastructure.Email;

namespace PropertyManagement.Infrastructure.Common.Security;

public sealed class HmacVerificationCodeProtector : IVerificationCodeProtector
{
    private readonly byte[] _hmacKey;

    public HmacVerificationCodeProtector(
        IOptions<EmailConfirmationOptions> confirmationOptions,
        IOptions<JwtOptions> jwtOptions)
    {
        var configuredSecret = confirmationOptions.Value.HmacSecret;
        if (!string.IsNullOrWhiteSpace(configuredSecret))
        {
            _hmacKey = SHA256.HashData(Encoding.UTF8.GetBytes(configuredSecret));
        }
        else
        {
            var jwtSecret = jwtOptions.Value.Secret;
            _hmacKey = SHA256.HashData(Encoding.UTF8.GetBytes($"PropertyManagement.EmailConfirmation.v1:{jwtSecret}"));
        }
    }

    public string Protect(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var hash = HMACSHA256.HashData(_hmacKey, Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(hash);
    }

    public bool Verify(string protectedCode, string providedCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(providedCode);

        var expectedHash = HMACSHA256.HashData(_hmacKey, Encoding.UTF8.GetBytes(providedCode));
        byte[] storedHash;
        try
        {
            storedHash = Convert.FromBase64String(protectedCode);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expectedHash, storedHash);
    }
}
