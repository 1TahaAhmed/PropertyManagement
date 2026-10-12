using MediatR;
using PropertyManagement.Application.Common.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace PropertyManagement.Application.Abstractions.EmailConfirmation.Verify
{
    public sealed record VerifyEmailConfirmationCommand(
        Guid UserId,
        string Code
    ) : IRequest<Result<VerifyEmailConfirmationResponse>>;
}
