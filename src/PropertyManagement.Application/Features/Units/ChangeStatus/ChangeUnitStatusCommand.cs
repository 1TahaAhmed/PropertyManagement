using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Units.ChangeStatus;

public sealed record ChangeUnitStatusCommand(
    Guid UnitId,
    UnitOperationalStatus NewStatus) : IRequest<Result>;