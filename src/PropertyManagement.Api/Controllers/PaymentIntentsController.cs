using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Authorization;
using PropertyManagement.Api.Contracts.Payments;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Application.Common.Security;
using PropertyManagement.Application.Features.Payments.Intents;
using PropertyManagement.Application.Features.Payments.Responses;

namespace PropertyManagement.Api.Controllers;

[Authorize]
[Route("api/v1/payment-intents")]
public sealed class PaymentIntentsController(
    ISender sender,
    ICurrentUser currentUser) : ApiControllerBase
{
    [HttpPost]
    [HasPermission(Permissions.Payments.Create)]
    [ProducesResponseType(typeof(PaymentIntentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePaymentIntentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorContext(currentUser, out var organizationId, out var actorUserId, out _, out var errorResult))
        {
            return errorResult!;
        }

        var idempotencyKey = PaymentHttpRequestHelper.ResolveIdempotencyKey(request.IdempotencyKey, Request);
        var command = new CreatePaymentIntentCommand(
            OrganizationId: organizationId,
            ActorUserId: actorUserId,
            ResidentId: request.ResidentId,
            Amount: request.Amount,
            Currency: request.Currency,
            PaymentMethod: request.PaymentMethod,
            Provider: request.Provider,
            LeaseId: request.LeaseId,
            TargetInvoiceId: request.TargetInvoiceId,
            Description: request.Description,
            IdempotencyKey: idempotencyKey,
            ExpiresInMinutes: request.ExpiresInMinutes ?? 30);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Payments.Read)]
    [ProducesResponseType(typeof(PaymentIntentResponse), StatusCodes.Status200OK)]
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

        var result = await sender.Send(new GetPaymentIntentByIdQuery(organizationId, id), cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }
}
