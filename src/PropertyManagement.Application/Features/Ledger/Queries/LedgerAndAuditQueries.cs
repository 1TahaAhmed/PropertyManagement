using MediatR;
using PropertyManagement.Application.Abstractions.Audit;
using PropertyManagement.Application.Abstractions.Ledger;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Payments.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Ledger.Queries;

public sealed record GetLedgerEntriesQuery(
    Guid OrganizationId,
    string? SourceType = null,
    Guid? SourceId = null,
    LedgerAccountCode? AccountCode = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<JournalEntryResponse>>>;

public sealed class GetLedgerEntriesQueryHandler(ILedgerRepository ledgerRepository)
    : IRequestHandler<GetLedgerEntriesQuery, Result<PagedList<JournalEntryResponse>>>
{
    public async Task<Result<PagedList<JournalEntryResponse>>> Handle(GetLedgerEntriesQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await ledgerRepository.GetPagedAsync(
            new LedgerFilter(
                request.OrganizationId,
                request.SourceType,
                request.SourceId,
                request.AccountCode,
                request.FromUtc,
                request.ToUtc,
                request.PageNumber,
                request.PageSize),
            cancellationToken);

        return Result<PagedList<JournalEntryResponse>>.Success(
            new PagedList<JournalEntryResponse>(
                items.Select(JournalEntryResponse.FromEntity).ToList(),
                request.PageNumber,
                request.PageSize,
                totalCount));
    }
}

public sealed record GetAccountBalancesQuery(Guid OrganizationId)
    : IRequest<Result<IReadOnlyList<AccountBalanceSummary>>>;

public sealed class GetAccountBalancesQueryHandler(ILedgerRepository ledgerRepository)
    : IRequestHandler<GetAccountBalancesQuery, Result<IReadOnlyList<AccountBalanceSummary>>>
{
    public async Task<Result<IReadOnlyList<AccountBalanceSummary>>> Handle(GetAccountBalancesQuery request, CancellationToken cancellationToken)
    {
        var balances = await ledgerRepository.GetAccountBalancesAsync(request.OrganizationId, cancellationToken);
        return Result<IReadOnlyList<AccountBalanceSummary>>.Success(balances);
    }
}

public sealed record GetFinancialAuditLogsQuery(
    Guid OrganizationId,
    string? EntityType = null,
    Guid? EntityId = null,
    string? Action = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<FinancialAuditLogResponse>>>;

public sealed class GetFinancialAuditLogsQueryHandler(IFinancialAuditLogRepository auditRepository)
    : IRequestHandler<GetFinancialAuditLogsQuery, Result<PagedList<FinancialAuditLogResponse>>>
{
    public async Task<Result<PagedList<FinancialAuditLogResponse>>> Handle(GetFinancialAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await auditRepository.GetPagedAsync(
            new FinancialAuditLogFilter(
                request.OrganizationId,
                request.EntityType,
                request.EntityId,
                request.Action,
                request.PageNumber,
                request.PageSize),
            cancellationToken);

        return Result<PagedList<FinancialAuditLogResponse>>.Success(
            new PagedList<FinancialAuditLogResponse>(
                items.Select(FinancialAuditLogResponse.FromEntity).ToList(),
                request.PageNumber,
                request.PageSize,
                totalCount));
    }
}
