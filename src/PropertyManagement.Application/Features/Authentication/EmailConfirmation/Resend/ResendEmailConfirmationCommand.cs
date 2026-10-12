using MediatR;
using PropertyManagement.Application.Common.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Application.Features.Authentication.EmailConfirmation.Resend
{
    public sealed record ResendEmailConfirmationCommand(
        Guid UserId,
        string IpAddress
    ) : IRequest<Result<ResendEmailConfirmationResponse>>;
}
