namespace PropertyManagement.Application.Abstractions.Security
{
    public interface IVerificationCodeGenerator
    {
        string Generate(int length);
    }
}
