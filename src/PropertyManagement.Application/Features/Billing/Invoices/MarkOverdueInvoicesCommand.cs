using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Billing.Responses;

namespace PropertyManagement.Application.Features.Billing.Invoices;

public sealed record MarkOverdueInvoicesCommand(
    Guid OrganizationId,
    DateOnly? AsOfDate = null) : IRequest<Result<MarkOverdueInvoicesResponse>>;
