using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Authorization;
using PropertyManagement.Api.Contracts.Billing;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Application.Common.Security;
using PropertyManagement.Application.Features.Billing.Charges;
using PropertyManagement.Application.Features.Billing.Generate;
using PropertyManagement.Application.Features.Billing.Preview;
using PropertyManagement.Application.Features.Billing.Responses;

namespace PropertyManagement.Api.Controllers;

[Authorize]
[Route("api/v1/billing")]
public sealed class BillingController(
    ISender sender,
    ICurrentUser currentUser) : ApiControllerBase
{
    [HttpPost("preview")]
    [HasPermission(Permissions.Billing.Preview)]
    [ProducesResponseType(typeof(BillingPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Preview(
        [FromBody] PreviewBillingRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var query = new PreviewBillingQuery(
            OrganizationId: organizationId,
            LeaseId: request.LeaseId,
            PeriodStart: request.PeriodStart,
            PeriodEnd: request.PeriodEnd,
            IssueDate: request.IssueDate,
            DueDateOverride: request.DueDateOverride,
            IncludeRent: request.IncludeRent,
            IncludePendingCharges: request.IncludePendingCharges,
            ChargeIds: request.ChargeIds,
            Discounts: request.Discounts,
            AdditionalCharges: request.AdditionalCharges);

        var result = await sender.Send(query, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("generate")]
    [HasPermission(Permissions.Billing.Generate)]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Generate(
        [FromBody] GenerateLeaseInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var command = new GenerateLeaseInvoiceCommand(
            OrganizationId: organizationId,
            LeaseId: request.LeaseId,
            PeriodStart: request.PeriodStart,
            PeriodEnd: request.PeriodEnd,
            IssueDate: request.IssueDate,
            DueDateOverride: request.DueDateOverride,
            IncludeRent: request.IncludeRent,
            IncludePendingCharges: request.IncludePendingCharges,
            ChargeIds: request.ChargeIds,
            Discounts: request.Discounts,
            AdditionalCharges: request.AdditionalCharges,
            Notes: request.Notes,
            AutoIssue: request.AutoIssue,
            IdempotencyKey: request.IdempotencyKey);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(
            nameof(InvoicesController.GetById),
            "Invoices",
            new { id = result.Value.Id },
            result.Value);
    }

    [HttpPost("charges")]
    [HasPermission(Permissions.Charges.Create)]
    [ProducesResponseType(typeof(ChargeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateCharge(
        [FromBody] CreateChargeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var command = new CreateChargeCommand(
            OrganizationId: organizationId,
            LeaseId: request.LeaseId,
            Type: request.Type,
            Description: request.Description,
            Quantity: request.Quantity,
            UnitPrice: request.UnitPrice,
            PeriodStart: request.PeriodStart,
            PeriodEnd: request.PeriodEnd);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("leases/{leaseId:guid}/charges/pending")]
    [HasPermission(Permissions.Charges.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<ChargeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPendingChargesByLease(
        [FromRoute] Guid leaseId,
        [FromQuery] DateOnly? upToPeriodEnd = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var query = new GetLeasePendingChargesQuery(organizationId, leaseId, upToPeriodEnd);
        var result = await sender.Send(query, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("charges/{chargeId:guid}/void")]
    [HasPermission(Permissions.Charges.Void)]
    [ProducesResponseType(typeof(ChargeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> VoidCharge(
        [FromRoute] Guid chargeId,
        [FromBody] VoidChargeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var command = new VoidChargeCommand(organizationId, chargeId, request.Reason);
        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }
}
