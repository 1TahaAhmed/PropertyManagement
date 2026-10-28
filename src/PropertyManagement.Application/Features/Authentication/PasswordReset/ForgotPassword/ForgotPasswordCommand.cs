using MediatR;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Authentication.PasswordReset.ForgotPassword;

public sealed record ForgotPasswordCommand(string Email) : IRequest<Result>;