namespace PropertyManagement.Api.Contracts.Authentication.PasswordReset;

public sealed record ResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword,
    string ConfirmPassword);