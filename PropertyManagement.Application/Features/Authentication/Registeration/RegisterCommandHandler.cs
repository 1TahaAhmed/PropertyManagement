using System.Text.Encodings.Web;
using MediatR;
using PropertyManagement.Application.Abstractions.Email;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Authentication.Registeration;

public sealed class RegisterCommandHandler(
    IIdentityService identityService,
    IEmailConfirmationLinkBuilder linkBuilder,
    IEmailSender emailSender)
    : IRequestHandler<RegisterCommand, Result<RegisterResponse>>
{
    public async Task<Result<RegisterResponse>> Handle(
        RegisterCommand request,
        CancellationToken cancellationToken)
    {
        var identityResult = await identityService.RegisterAsync(
            request.FirstName,
            request.LastName,
            request.Address,
            request.Email,
            request.Password,
            cancellationToken);

        if (identityResult.IsFailure)
        {
            return Result<RegisterResponse>.Failure(
                identityResult.Errors.ToArray());
        }

        var identityUser = identityResult.Value;

        var tokenResult =
            await identityService.GenerateEmailConfirmationTokenAsync(
                identityUser.UserId,
                cancellationToken);

        if (tokenResult.IsFailure)
        {
            return Result<RegisterResponse>.Failure(
                tokenResult.Errors.ToArray());
        }

        var confirmationLink = linkBuilder.Build(
            identityUser.UserId,
            tokenResult.Value);

        var emailBody = BuildConfirmationEmailBody(
            request.FirstName,
            confirmationLink);

        await emailSender.SendAsync(
            recipient: request.Email,
            subject: "Confirm your email address",
            body: emailBody,
            cancellationToken);

        var response = new RegisterResponse(
            UserId: identityUser.UserId,
            Email: request.Email,
            FirstName: request.FirstName,
            LastName: request.LastName,
            EmailConfirmed: false,
            CreatedAt: identityUser.CreatedAt);

        return Result<RegisterResponse>.Success(response);
    }

    private static string BuildConfirmationEmailBody(
        string firstName,
        string confirmationLink)
    {
        var encodedFirstName =
            HtmlEncoder.Default.Encode(firstName);

        var encodedConfirmationLink =
            HtmlEncoder.Default.Encode(confirmationLink);

        return $"""
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <title>Confirm your email</title>
            </head>
            <body>
                <h2>Welcome, {encodedFirstName}</h2>

                <p>
                    Please confirm your email address by clicking
                    the link below:
                </p>

                <p>
                    <a href="{encodedConfirmationLink}">
                        Confirm email address
                    </a>
                </p>

                <p>
                    If you did not create this account, you can ignore
                    this email.
                </p>
            </body>
            </html>
            """;
    }
}
