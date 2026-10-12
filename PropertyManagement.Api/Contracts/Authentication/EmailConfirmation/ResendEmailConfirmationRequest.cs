namespace PropertyManagement.Api.Contracts.Authentication.EmailConfirmation
{
    public sealed record ResendEmailConfirmationRequest(
        Guid UserId);
}
