using PropertyManagement.Application.Features.Leases.Cancel;
using PropertyManagement.Application.Features.Leases.GetPaged;
using PropertyManagement.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Api.Authorization;
using PropertyManagement.Api.Contracts.Leases;
using PropertyManagement.Api.Extensions;
using PropertyManagement.Application.Abstractions.Security;
using PropertyManagement.Application.Common.Security;
using PropertyManagement.Application.Features.Leases.AddResident;
using PropertyManagement.Application.Features.Leases.Approve;
using PropertyManagement.Application.Features.Leases.CreateDraft;
using PropertyManagement.Application.Features.Leases.GetById;
using PropertyManagement.Application.Features.Leases.Renew;
using PropertyManagement.Application.Features.Leases.Responses;
using PropertyManagement.Application.Features.Leases.Submit;
using PropertyManagement.Application.Features.Leases.Terminate;

namespace PropertyManagement.Api.Controllers;

[Route("api/v1/leases")]
[Authorize]
public sealed class LeasesController(
    ISender sender,
    ICurrentUser currentUser) : ApiControllerBase
{
    private Guid? CurrentOrgId => currentUser.OrganizationId;

    [HttpPost]
    [HasPermission(Permissions.Leases.Create)]
    [ProducesResponseType(typeof(LeaseResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreateDraftLeaseRequest request,
        CancellationToken cancellationToken)
    {
        if (!CurrentOrgId.HasValue)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "An active organization context is required."
            });
        }

        var command = new CreateDraftLeaseCommand(
            OrganizationId: CurrentOrgId.Value,
            PropertyId: request.PropertyId,
            UnitId: request.UnitId,
            StartDate: request.StartDate,
            EndDate: request.EndDate,
            RentAmount: request.RentAmount,
            SecurityDeposit: request.SecurityDeposit,
            Frequency: request.Frequency,
            DueDayOfMonth: request.DueDayOfMonth,
            GracePeriodDays: request.GracePeriodDays,
            LateFeePercentage: request.LateFeePercentage,
            PrimaryResidentId: request.PrimaryResidentId);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Leases.Read)]
    [ProducesResponseType(typeof(LeaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!CurrentOrgId.HasValue)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "An active organization context is required."
            });
        }

        var query = new GetLeaseByIdQuery(CurrentOrgId.Value, id);
        var result = await sender.Send(query, cancellationToken);
        
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpGet]
    [HasPermission(Permissions.Leases.Read)]
    [ProducesResponseType(typeof(PagedLeaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] Guid? propertyId,
        [FromQuery] Guid? unitId,
        [FromQuery] LeaseStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!CurrentOrgId.HasValue)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "An active organization context is required."
            });
        }

        var query = new GetLeasesPagedQuery(CurrentOrgId.Value, propertyId, unitId, status, pageNumber, pageSize);
        var result = await sender.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(Permissions.Leases.Cancel)]
    [ProducesResponseType(typeof(LeaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!CurrentOrgId.HasValue)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "An active organization context is required."
            });
        }

        var command = new CancelLeaseCommand(CurrentOrgId.Value, id);
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/residents")]
    [HasPermission(Permissions.Leases.Update)]
    [ProducesResponseType(typeof(LeaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddResident(
        [FromRoute] Guid id,
        [FromBody] AddResidentToLeaseRequest request,
        CancellationToken cancellationToken)
    {
        if (!CurrentOrgId.HasValue)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "An active organization context is required."
            });
        }

        var command = new AddResidentToLeaseCommand(CurrentOrgId.Value, id, request.ResidentId, request.Role);
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/submit")]
    [HasPermission(Permissions.Leases.Submit)]
    [ProducesResponseType(typeof(LeaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitForApproval(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!CurrentOrgId.HasValue)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "An active organization context is required."
            });
        }

        var command = new SubmitLeaseForApprovalCommand(CurrentOrgId.Value, id);
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/approve")]
    [HasPermission(Permissions.Leases.Approve)]
    [ProducesResponseType(typeof(LeaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!CurrentOrgId.HasValue)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "An active organization context is required."
            });
        }

        var command = new ApproveLeaseCommand(CurrentOrgId.Value, id);
        var result = await sender.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/terminate")]
    [HasPermission(Permissions.Leases.Terminate)]
    [ProducesResponseType(typeof(LeaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Terminate(
        [FromRoute] Guid id,
        [FromBody] TerminateLeaseRequest request,
        CancellationToken cancellationToken)
    {
        if (!CurrentOrgId.HasValue)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "An active organization context is required."
            });
        }

        var userId = currentUser.UserId ?? Guid.Empty;
        if (userId == Guid.Empty)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Authenticated user id is required for termination."
            });
        }

        var command = new TerminateLeaseCommand(
            OrganizationId: CurrentOrgId.Value,
            LeaseId: id,
            TerminationDate: request.TerminationDate,
            Reason: request.Reason,
            TerminatedByUserId: userId,
            Notes: request.Notes);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/renew")]
    [HasPermission(Permissions.Leases.Renew)]
    [ProducesResponseType(typeof(LeaseResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Renew(
        [FromRoute] Guid id,
        [FromBody] RenewLeaseRequest request,
        CancellationToken cancellationToken)
    {
        if (!CurrentOrgId.HasValue)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "An active organization context is required."
            });
        }

        var command = new RenewLeaseCommand(
            OrganizationId: CurrentOrgId.Value,
            LeaseId: id,
            NewEndDate: request.NewEndDate,
            NewRentAmount: request.NewRentAmount,
            NewSecurityDeposit: request.NewSecurityDeposit,
            NewFrequency: request.NewFrequency,
            NewDueDayOfMonth: request.NewDueDayOfMonth,
            NewGracePeriodDays: request.NewGracePeriodDays,
            NewLateFeePercentage: request.NewLateFeePercentage);

        var result = await sender.Send(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }
}
