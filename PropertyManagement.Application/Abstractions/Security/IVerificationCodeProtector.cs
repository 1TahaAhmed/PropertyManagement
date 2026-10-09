namespace PropertyManagement.Application.Abstractions.Security
{
    public interface IVerificationCodeProtector
    {
        string Protect(string code);

        bool Verify(
            string protectedCode,
            string providedCode);
    }
}
