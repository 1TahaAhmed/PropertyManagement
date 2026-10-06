namespace PropertyManagement.Api.Contracts.Authentication
{
    public sealed record RegisterRequest(
        string FirstName,
        string LastName,
        string Address,
        string Email,
        string Password,
        string ConfirmPassword
    );
}
