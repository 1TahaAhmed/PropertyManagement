namespace PropertyManagement.Application.Abstractions.Payments;

public interface IPaymentNumberGenerator
{
    Task<string> GenerateNextAsync(Guid organizationId, DateTime utcNow, CancellationToken cancellationToken = default);
}
