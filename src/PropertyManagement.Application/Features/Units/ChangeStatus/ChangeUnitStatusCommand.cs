using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Units.ChangeStatus;

public sealed record ChangeUnitStatusCommand(
    Guid UnitId,
    Guid OrganizationId,
    UnitOperationalStatus NewStatus) : IRequest<Result>;
