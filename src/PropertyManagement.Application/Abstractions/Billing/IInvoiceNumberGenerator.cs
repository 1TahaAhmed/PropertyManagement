namespace PropertyManagement.Application.Abstractions.Billing;

public interface IInvoiceNumberGenerator
{
    Task<string> GenerateNextAsync(Guid organizationId, int year, CancellationToken cancellationToken = default);
}
