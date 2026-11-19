using System.Data;
using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Audit;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Abstractions.Ledger;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Common;
using PropertyManagement.Application.Features.Billing.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Billing.Invoices;

public sealed class IssueInvoiceCommandHandler(
    IInvoiceRepository invoiceRepository,
    IInvoiceNumberGenerator invoiceNumberGenerator,
    ILedgerRepository ledgerRepository,
    IJournalEntryNumberGenerator journalEntryNumberGenerator,
    IFinancialAuditLogRepository auditRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<IssueInvoiceCommand, Result<InvoiceResponse>>
{
    public async Task<Result<InvoiceResponse>> Handle(IssueInvoiceCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await invoiceRepository.AcquireInvoiceLockAsync(request.InvoiceId, request.OrganizationId, cancellationToken);
        var invoice = await invoiceRepository.GetByIdAsync(request.InvoiceId, request.OrganizationId, cancellationToken);
        if (invoice is null || invoice.OrganizationId != request.OrganizationId)
        {
            return Result<InvoiceResponse>.Failure(new Error(
                Code: "Invoice.NotFound",
                Message: "The specified invoice was not found.",
                Type: ErrorType.NotFound));
        }

        if (invoice.Status != InvoiceStatus.Draft)
        {
            return Result<InvoiceResponse>.Failure(new Error(
                Code: "Invoice.InvalidState",
                Message: $"Cannot issue invoice in '{invoice.Status}' status. Only Draft invoices can be issued.",
                Type: ErrorType.Conflict));
        }

        if (!string.IsNullOrWhiteSpace(request.RowVersion))
        {
            try
            {
                invoiceRepository.SetRowVersion(invoice, Convert.FromBase64String(request.RowVersion));
            }
            catch (FormatException)
            {
                return Result<InvoiceResponse>.Failure(new Error(
                    Code: "Invoice.InvalidRowVersion",
                    Message: "The provided RowVersion concurrency token is invalid.",
                    Type: ErrorType.Validation));
            }
        }

        var utcNow = clock.UtcNow;
        var issueDate = request.IssueDate ?? invoice.IssueDate;
        try
        {
            var invoiceNumber = await invoiceNumberGenerator.GenerateNextAsync(
                request.OrganizationId,
                issueDate.Year,
                cancellationToken);
            invoice.Issue(invoiceNumber, issueDate, utcNow);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result<InvoiceResponse>.Failure(new Error(
                Code: "Invoice.IssueError",
                Message: ex.Message,
                Type: ErrorType.Validation));
        }

        await BillingLedgerHelper.PostInvoiceIssuedIfNeededAsync(
            invoice,
            ledgerRepository,
            journalEntryNumberGenerator,
            auditRepository,
            actorUserId: null,
            utcNow,
            cancellationToken);

        invoiceRepository.Update(invoice);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<InvoiceResponse>.Success(InvoiceResponse.FromEntity(invoice));
    }
}
