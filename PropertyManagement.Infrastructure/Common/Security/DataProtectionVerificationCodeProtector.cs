using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using PropertyManagement.Application.Abstractions.Security;

namespace PropertyManagement.Infrastructure.Common.Security;

public sealed class DataProtectionVerificationCodeProtector
    : IVerificationCodeProtector
{
    private const string ProtectorPurpose =
        "PropertyManagement.EmailConfirmation.VerificationCode.v1";

    private readonly IDataProtector _protector;

    public DataProtectionVerificationCodeProtector(
        IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(
            ProtectorPurpose);
    }

    public string Protect(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return _protector.Protect(code);
    }

    public bool Verify(
        string protectedCode,
        string providedCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            protectedCode);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            providedCode);

        try
        {
            var originalCode = _protector.Unprotect(
                protectedCode);

            var originalBytes = Encoding.UTF8.GetBytes(
                originalCode);

            var providedBytes = Encoding.UTF8.GetBytes(
                providedCode);

            return CryptographicOperations.FixedTimeEquals(
                originalBytes,
                providedBytes);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
