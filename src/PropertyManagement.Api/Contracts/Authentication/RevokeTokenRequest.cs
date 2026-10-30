namespace PropertyManagement.Api.Contracts.Authentication;

public sealed record RevokeTokenRequest(
    string RefreshToken);