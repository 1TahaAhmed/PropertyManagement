using MediatR;
using PropertyManagement.Application.Abstractions.Billing;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Responses;

namespace PropertyManagement.Application.Features.Billing.Invoices;

public sealed class GetInvoicesQueryHandler(
    IInvoiceRepository invoiceRepository) : IRequestHandler<GetInvoicesQuery, Result<PagedInvoiceResponse>>
{
    public async Task<Result<PagedInvoiceResponse>> Handle(GetInvoicesQuery request, CancellationToken cancellationToken)
    {
        var filter = new InvoiceFilter(
            OrganizationId: request.OrganizationId,
            LeaseId: request.LeaseId,
            ResidentId: request.ResidentId,
            PropertyId: request.PropertyId,
            UnitId: request.UnitId,
            Status: request.Status,
            OnlyOverdue: request.OnlyOverdue,
            DueBefore: request.DueBefore,
            PeriodStart: request.PeriodStart,
            PeriodEnd: request.PeriodEnd,
            InvoiceNumber: request.InvoiceNumber,
            PageNumber: request.PageNumber,
            PageSize: request.PageSize);

        var (items, totalCount) = await invoiceRepository.GetPagedAsync(filter, cancellationToken);

        return Result<PagedInvoiceResponse>.Success(new PagedInvoiceResponse(
            Items: items.Select(InvoiceSummaryResponse.FromEntity).ToList(),
            TotalCount: totalCount,
            PageNumber: request.PageNumber,
            PageSize: request.PageSize));
    }
}
