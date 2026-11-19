using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Authorization;
using PropertyManagement.Api.Contracts.Payments;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Common.Security;
using PropertyManagement.Application.Features.Payments.Allocations;
using PropertyManagement.Application.Features.Payments.Lifecycle;
using PropertyManagement.Application.Features.Payments.Queries;
using PropertyManagement.Application.Features.Payments.Reconciliation;
using PropertyManagement.Application.Features.Payments.Refunds;
using PropertyManagement.Application.Features.Payments.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Controllers;

[Authorize]
[Route("api/v1/payments")]
public sealed class PaymentsController(
    ISender sender,
    ICurrentUser currentUser) : ApiControllerBase
{
    [HttpPost]
    [HasPermission(Permissions.Payments.Create)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorContext(currentUser, out var organizationId, out var actorUserId, out _, out var errorResult))
        {
            return errorResult!;
        }

        var idempotencyKey = PaymentHttpRequestHelper.ResolveIdempotencyKey(request.IdempotencyKey, Request);
        var command = new CreatePaymentCommand(
            OrganizationId: organizationId,
            ActorUserId: actorUserId,
            ResidentId: request.ResidentId,
            Amount: request.Amount,
            Currency: request.Currency,
            PaymentMethod: request.PaymentMethod,
            Provider: request.Provider,
            LeaseId: request.LeaseId,
            PaymentIntentId: request.PaymentIntentId,
            AllocationStrategy: request.AllocationStrategy,
            OverpaymentPolicy: request.OverpaymentPolicy,
            Allocations: request.Allocations,
            Reference: request.Reference,
            MetadataJson: request.MetadataJson,
            IdempotencyKey: idempotencyKey,
            ReceivedAtUtc: request.ReceivedAtUtc);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet]
    [HasPermission(Permissions.Payments.Read)]
    [ProducesResponseType(typeof(PagedList<PaymentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] Guid? residentId = null,
        [FromQuery] Guid? leaseId = null,
        [FromQuery] PaymentStatus? status = null,
        [FromQuery] PaymentMethod? paymentMethod = null,
        [FromQuery] PaymentProviderType? provider = null,
        [FromQuery] bool onlyUnapplied = false,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var query = new GetPaymentsQuery(
            OrganizationId: organizationId,
            ResidentId: residentId,
            LeaseId: leaseId,
            Status: status,
            PaymentMethod: paymentMethod,
            Provider: provider,
            OnlyUnapplied: onlyUnapplied,
            PageNumber: pageNumber,
            PageSize: pageSize);

        var result = await sender.Send(query, cancellationToken);
        return result.IsFailure ? this.ToActionResult(result) : Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Payments.Read)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
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

        var result = await sender.Send(new GetPaymentByIdQuery(organizationId, id), cancellationToken);
        return result.IsFailure ? this.ToActionResult(result) : Ok(result.Value);
    }

    [HttpPost("{id:guid}/confirm")]
    [HasPermission(Permissions.Payments.Confirm)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(
        [FromRoute] Guid id,
        [FromBody] ConfirmPaymentRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActorContext(currentUser, out var organizationId, out var actorUserId, out _, out var errorResult))
        {
            return errorResult!;
        }

        var rowVersion = PaymentHttpRequestHelper.ResolveRowVersion(request?.RowVersion, Request);
        var command = new ConfirmPaymentCommand(
            OrganizationId: organizationId,
            ActorUserId: actorUserId,
            PaymentId: id,
            ProviderTransactionId: request?.ProviderTransactionId,
            AllocationStrategy: request?.AllocationStrategy ?? AllocationStrategy.Explicit,
            Allocations: request?.Allocations,
            RowVersion: rowVersion);

        var result = await sender.Send(command, cancellationToken);
        return result.IsFailure ? this.ToActionResult(result) : Ok(result.Value);
    }

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(Permissions.Payments.Cancel)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        [FromRoute] Guid id,
        [FromBody] CancelPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorContext(currentUser, out var organizationId, out var actorUserId, out _, out var errorResult))
        {
            return errorResult!;
        }

        var rowVersion = PaymentHttpRequestHelper.ResolveRowVersion(request.RowVersion, Request);
        var command = new CancelPaymentCommand(
            OrganizationId: organizationId,
            ActorUserId: actorUserId,
            PaymentId: id,
            Reason: request.Reason,
            RowVersion: rowVersion);

        var result = await sender.Send(command, cancellationToken);
        return result.IsFailure ? this.ToActionResult(result) : Ok(result.Value);
    }

    [HttpPost("{id:guid}/sync-status")]
    [HasPermission(Permissions.Payments.Confirm)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SyncStatus(
        [FromRoute] Guid id,
        [FromBody] SyncPaymentStatusRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActorContext(currentUser, out var organizationId, out var actorUserId, out _, out var errorResult))
        {
            return errorResult!;
        }

        var command = new SyncPaymentStatusCommand(
            OrganizationId: organizationId,
            ActorUserId: actorUserId,
            PaymentId: id,
            AutoAllocate: request?.AutoAllocate ?? true);

        var result = await sender.Send(command, cancellationToken);
        return result.IsFailure ? this.ToActionResult(result) : Ok(result.Value);
    }

    [HttpPost("{id:guid}/allocations")]
    [HasPermission(Permissions.Payments.Allocate)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Allocate(
        [FromRoute] Guid id,
        [FromBody] AllocatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorContext(currentUser, out var organizationId, out var actorUserId, out _, out var errorResult))
        {
            return errorResult!;
        }

        var rowVersion = PaymentHttpRequestHelper.ResolveRowVersion(request.RowVersion, Request);
        var command = new AllocatePaymentCommand(
            OrganizationId: organizationId,
            ActorUserId: actorUserId,
            PaymentId: id,
            Strategy: request.Strategy,
            Allocations: request.Allocations,
            RowVersion: rowVersion);

        var result = await sender.Send(command, cancellationToken);
        return result.IsFailure ? this.ToActionResult(result) : Ok(result.Value);
    }

    [HttpPost("{id:guid}/allocations/{allocationId:guid}/reverse")]
    [HasPermission(Permissions.Payments.ReverseAllocation)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReverseAllocation(
        [FromRoute] Guid id,
        [FromRoute] Guid allocationId,
        [FromBody] ReversePaymentAllocationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorContext(currentUser, out var organizationId, out var actorUserId, out _, out var errorResult))
        {
            return errorResult!;
        }

        var rowVersion = PaymentHttpRequestHelper.ResolveRowVersion(request.RowVersion, Request);
        var command = new ReversePaymentAllocationCommand(
            OrganizationId: organizationId,
            ActorUserId: actorUserId,
            PaymentId: id,
            AllocationId: allocationId,
            Reason: request.Reason,
            RowVersion: rowVersion);

        var result = await sender.Send(command, cancellationToken);
        return result.IsFailure ? this.ToActionResult(result) : Ok(result.Value);
    }

    [HttpPost("{id:guid}/refunds")]
    [HasPermission(Permissions.Payments.Refund)]
    [ProducesResponseType(typeof(RefundResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Refund(
        [FromRoute] Guid id,
        [FromBody] RefundPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorContext(currentUser, out var organizationId, out var actorUserId, out _, out var errorResult))
        {
            return errorResult!;
        }

        var idempotencyKey = PaymentHttpRequestHelper.ResolveIdempotencyKey(request.IdempotencyKey, Request);
        var rowVersion = PaymentHttpRequestHelper.ResolveRowVersion(request.RowVersion, Request);
        var command = new RefundPaymentCommand(
            OrganizationId: organizationId,
            ActorUserId: actorUserId,
            PaymentId: id,
            Amount: request.Amount,
            Reason: request.Reason,
            IdempotencyKey: idempotencyKey,
            RowVersion: rowVersion);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("{id:guid}/refunds")]
    [HasPermission(Permissions.Payments.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<RefundResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRefunds(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var result = await sender.Send(new GetPaymentRefundsQuery(organizationId, id), cancellationToken);
        return result.IsFailure ? this.ToActionResult(result) : Ok(result.Value);
    }

    [HttpPost("{id:guid}/settle")]
    [HasPermission(Permissions.Payments.Reconcile)]
    [ProducesResponseType(typeof(PaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordSettlement(
        [FromRoute] Guid id,
        [FromBody] RecordProviderSettlementRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorContext(currentUser, out var organizationId, out var actorUserId, out _, out var errorResult))
        {
            return errorResult!;
        }

        var command = new RecordProviderSettlementCommand(
            OrganizationId: organizationId,
            ActorUserId: actorUserId,
            PaymentId: id,
            ProviderFee: request.ProviderFee,
            SettledAtUtc: request.SettledAtUtc);

        var result = await sender.Send(command, cancellationToken);
        return result.IsFailure ? this.ToActionResult(result) : Ok(result.Value);
    }

    [HttpPost("reconcile")]
    [HasPermission(Permissions.Payments.Reconcile)]
    [ProducesResponseType(typeof(ReconciliationBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Reconcile(
        [FromBody] ReconcilePaymentsRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorContext(currentUser, out var organizationId, out var actorUserId, out _, out var errorResult))
        {
            return errorResult!;
        }

        var command = new ReconcilePaymentsCommand(
            OrganizationId: organizationId,
            ActorUserId: actorUserId,
            Provider: request.Provider,
            FromUtc: request.FromUtc,
            ToUtc: request.ToUtc,
            ProviderTransactionsOverride: request.ProviderTransactionsOverride);

        var result = await sender.Send(command, cancellationToken);
        return result.IsFailure ? this.ToActionResult(result) : Ok(result.Value);
    }

    [HttpGet("reconciliation")]
    [HasPermission(Permissions.Payments.Reconcile)]
    [ProducesResponseType(typeof(PagedList<ReconciliationRecordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReconciliationRecords(
        [FromQuery] Guid? batchId = null,
        [FromQuery] PaymentProviderType? provider = null,
        [FromQuery] ReconciliationStatus? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetOrganizationId(currentUser, out var organizationId, out var errorResult))
        {
            return errorResult!;
        }

        var query = new GetReconciliationRecordsQuery(
            OrganizationId: organizationId,
            BatchId: batchId,
            Provider: provider,
            Status: status,
            PageNumber: pageNumber,
            PageSize: pageSize);

        var result = await sender.Send(query, cancellationToken);
        return result.IsFailure ? this.ToActionResult(result) : Ok(result.Value);
    }

    [HttpPost("reconciliation/{recordId:guid}/resolve")]
    [HasPermission(Permissions.Payments.Reconcile)]
    [ProducesResponseType(typeof(ReconciliationRecordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResolveReconciliationRecord(
        [FromRoute] Guid recordId,
        [FromBody] ResolveReconciliationRecordRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorContext(currentUser, out var organizationId, out var actorUserId, out _, out var errorResult))
        {
            return errorResult!;
        }

        var command = new ResolveReconciliationRecordCommand(
            OrganizationId: organizationId,
            ActorUserId: actorUserId,
            RecordId: recordId,
            ResolutionNotes: request.ResolutionNotes);

        var result = await sender.Send(command, cancellationToken);
        return result.IsFailure ? this.ToActionResult(result) : Ok(result.Value);
    }
}
