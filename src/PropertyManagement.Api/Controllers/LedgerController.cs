using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Authorization;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Abstractions.Ledger;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Common.Security;
using PropertyManagement.Application.Features.Ledger.Queries;
using PropertyManagement.Application.Features.Payments.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Controllers;

[Authorize]
[Route("api/v1/ledger")]
public sealed class LedgerController(
    ISender sender,
    ICurrentUser currentUser) : ApiControllerBase
{
    [HttpGet("entries")]
    [HasPermission(Permissions.Ledger.Read)]
    [ProducesResponseType(typeof(PagedList<JournalEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetEntries(
        [FromQuery] string? sourceType = null,
        [FromQuery] Guid? sourceId = null,
        [FromQuery] LedgerAccountCode? accountCode = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var query = new GetLedgerEntriesQuery(
            OrganizationId: organizationId,
            SourceType: sourceType,
            SourceId: sourceId,
            AccountCode: accountCode,
            FromUtc: fromUtc,
            ToUtc: toUtc,
            PageNumber: pageNumber,
            PageSize: pageSize);

        var result = await sender.Send(query, cancellationToken);
        return result.IsFailure ? this.ToActionResult(result) : Ok(result.Value);
    }

    [HttpGet("balances")]
    [HasPermission(Permissions.Ledger.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<AccountBalanceSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetBalances(CancellationToken cancellationToken)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var result = await sender.Send(new GetAccountBalancesQuery(organizationId), cancellationToken);
        return result.IsFailure ? this.ToActionResult(result) : Ok(result.Value);
    }

    [HttpGet("audit-logs")]
    [HasPermission(Permissions.Audit.Read)]
    [ProducesResponseType(typeof(PagedList<FinancialAuditLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? entityType = null,
        [FromQuery] Guid? entityId = null,
        [FromQuery] string? action = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var query = new GetFinancialAuditLogsQuery(
            OrganizationId: organizationId,
            EntityType: entityType,
            EntityId: entityId,
            Action: action,
            PageNumber: pageNumber,
            PageSize: pageSize);

        var result = await sender.Send(query, cancellationToken);
        return result.IsFailure ? this.ToActionResult(result) : Ok(result.Value);
    }
}
