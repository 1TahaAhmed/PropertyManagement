using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Abstractions.Email;

public interface IEmailConfirmationResendPolicy
{
    Task<Result> TryAcquireAsync(
        Guid userId,
        string ipAddress,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}
