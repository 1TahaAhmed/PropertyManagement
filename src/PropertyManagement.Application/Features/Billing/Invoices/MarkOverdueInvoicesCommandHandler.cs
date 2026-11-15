using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Billing.Invoices;

public sealed class MarkOverdueInvoicesCommandHandler(
    IInvoiceRepository invoiceRepository,
    IUnitOfWork unitOfWork,
    IClock clock) : IRequestHandler<MarkOverdueInvoicesCommand, Result<MarkOverdueInvoicesResponse>>
{
    public async Task<Result<MarkOverdueInvoicesResponse>> Handle(
        MarkOverdueInvoicesCommand request,
        CancellationToken cancellationToken)
    {
        var utcNow = clock.UtcNow;
        var effectiveAsOfDate = request.AsOfDate ?? DateOnly.FromDateTime(utcNow);
        var dueBefore = effectiveAsOfDate.AddDays(-1);

        var markedIds = new List<Guid>();
        const int pageSize = 200;

        foreach (var candidateStatus in new[] { InvoiceStatus.Issued, InvoiceStatus.PartiallyPaid })
        {
            var pageNumber = 1;
            while (true)
            {
                var (items, totalCount) = await invoiceRepository.GetPagedAsync(
                    new InvoiceFilter(
                        OrganizationId: request.OrganizationId,
                        Status: candidateStatus,
                        DueBefore: dueBefore,
                        PageNumber: pageNumber,
                        PageSize: pageSize),
                    cancellationToken);

                foreach (var invoice in items)
                {
                    if (invoice.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid &&
                        invoice.BalanceDue > 0m &&
                        invoice.DueDate < effectiveAsOfDate)
                    {
                        invoice.MarkOverdue(effectiveAsOfDate, utcNow);
                        invoiceRepository.Update(invoice);
                        markedIds.Add(invoice.Id);
                    }
                }

                if (pageNumber * pageSize >= totalCount || items.Count == 0)
                {
                    break;
                }

                pageNumber++;
            }
        }

        if (markedIds.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<MarkOverdueInvoicesResponse>.Success(
            new MarkOverdueInvoicesResponse(markedIds.Count, markedIds));
    }
}
