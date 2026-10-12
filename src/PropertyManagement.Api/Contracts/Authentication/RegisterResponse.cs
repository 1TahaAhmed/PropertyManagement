namespace PropertyManagement.Api.Contracts.Authentication
{
    public sealed record RegisterResponse(
        Guid UserId,
        string Email,
        string FirstName,
        string LastName,
        bool EmailConfirmed,
        DateTime CreatedAt);
}
