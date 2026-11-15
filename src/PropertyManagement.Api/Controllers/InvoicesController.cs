using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Authorization;
using PropertyManagement.Api.Contracts.Billing;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Application.Common.Security;
using PropertyManagement.Application.Features.Billing.Invoices;
using PropertyManagement.Application.Features.Billing.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Controllers;

[Authorize]
[Route("api/v1/invoices")]
public sealed class InvoicesController(
    ISender sender,
    ICurrentUser currentUser) : ApiControllerBase
{
    [HttpPost]
    [HasPermission(Permissions.Invoices.Create)]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateManual(
        [FromBody] CreateManualInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var command = new CreateManualInvoiceCommand(
            OrganizationId: organizationId,
            LeaseId: request.LeaseId,
            PeriodStart: request.PeriodStart,
            PeriodEnd: request.PeriodEnd,
            DueDate: request.DueDate,
            Lines: request.Lines,
            ChargeIds: request.ChargeIds,
            IssueDate: request.IssueDate,
            Notes: request.Notes,
            AutoIssue: request.AutoIssue,
            IdempotencyKey: request.IdempotencyKey);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet]
    [HasPermission(Permissions.Invoices.Read)]
    [ProducesResponseType(typeof(PagedInvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] Guid? leaseId = null,
        [FromQuery] Guid? residentId = null,
        [FromQuery] Guid? propertyId = null,
        [FromQuery] Guid? unitId = null,
        [FromQuery] InvoiceStatus? status = null,
        [FromQuery] bool onlyOverdue = false,
        [FromQuery] DateOnly? dueBefore = null,
        [FromQuery] DateOnly? periodStart = null,
        [FromQuery] DateOnly? periodEnd = null,
        [FromQuery] string? invoiceNumber = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var query = new GetInvoicesQuery(
            OrganizationId: organizationId,
            LeaseId: leaseId,
            ResidentId: residentId,
            PropertyId: propertyId,
            UnitId: unitId,
            Status: status,
            OnlyOverdue: onlyOverdue,
            DueBefore: dueBefore,
            PeriodStart: periodStart,
            PeriodEnd: periodEnd,
            InvoiceNumber: invoiceNumber,
            PageNumber: pageNumber,
            PageSize: pageSize);

        var result = await sender.Send(query, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Invoices.Read)]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var result = await sender.Send(new GetInvoiceByIdQuery(organizationId, id), cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/issue")]
    [HasPermission(Permissions.Invoices.Issue)]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Issue(
        [FromRoute] Guid id,
        [FromBody] IssueInvoiceRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var command = new IssueInvoiceCommand(
            OrganizationId: organizationId,
            InvoiceId: id,
            IssueDate: request?.IssueDate,
            RowVersion: request?.RowVersion);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/void")]
    [HasPermission(Permissions.Invoices.Void)]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Void(
        [FromRoute] Guid id,
        [FromBody] VoidInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var command = new VoidInvoiceCommand(
            OrganizationId: organizationId,
            InvoiceId: id,
            Reason: request.Reason,
            RowVersion: request.RowVersion);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/adjustments")]
    [HasPermission(Permissions.Invoices.Adjust)]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApplyAdjustment(
        [FromRoute] Guid id,
        [FromBody] ApplyInvoiceAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorContext(currentUser, out var organizationId, out var actorUserId, out _, out var errorResult))
        {
            return errorResult!;
        }

        var command = new ApplyInvoiceAdjustmentCommand(
            OrganizationId: organizationId,
            InvoiceId: id,
            Type: request.Type,
            ChargeType: request.ChargeType,
            Amount: request.Amount,
            Description: request.Description,
            Reason: request.Reason,
            ActorUserId: actorUserId,
            RowVersion: request.RowVersion);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("mark-overdue")]
    [HasPermission(Permissions.Invoices.MarkOverdue)]
    [ProducesResponseType(typeof(MarkOverdueInvoicesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkOverdue(
        [FromBody] MarkOverdueInvoicesRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var command = new MarkOverdueInvoicesCommand(
            OrganizationId: organizationId,
            AsOfDate: request?.AsOfDate);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }
}
