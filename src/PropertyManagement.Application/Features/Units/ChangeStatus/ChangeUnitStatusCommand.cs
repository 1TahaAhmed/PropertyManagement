using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Units.ChangeStatus;

public sealed record ChangeUnitStatusCommand(
    Guid UnitId,
    UnitOperationalStatus NewStatus) : IRequest<Result>;

public sealed class ChangeUnitStatusCommandHandler(
    IUnitRepository unitRepository,
    IClock clock)
    : IRequestHandler<ChangeUnitStatusCommand, Result>
{
    public async Task<Result> Handle(ChangeUnitStatusCommand request, CancellationToken cancellationToken)
    {
        var unit = await unitRepository.GetByIdAsync(request.UnitId, cancellationToken);
        if (unit is null)
        {
            return Result.Failure(new Error(
                Code: "Unit.NotFound",
                Message: "Unit was not found.",
                Type: ErrorType.NotFound));
        }

        unit.SetStatus(request.NewStatus, clock.UtcNow);
        await unitRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}