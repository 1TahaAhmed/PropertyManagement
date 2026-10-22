using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Units.ChangeStatus;

public sealed class ChangeUnitStatusCommandHandler(
    IPropertyRepository propertyRepository,
    IUnitRepository unitRepository,
    IClock clock)
    : IRequestHandler<ChangeUnitStatusCommand, Result>
{
    public async Task<Result> Handle(ChangeUnitStatusCommand request, CancellationToken cancellationToken)
    {
        var unit = await unitRepository.GetByIdAsync(request.UnitId, cancellationToken);
        if (unit is null || unit.TenantId != request.TenantId)
        {
            return Result.Failure(new Error(
                Code: "Unit.NotFound",
                Message: "Unit was not found.",
                Type: ErrorType.NotFound));
        }

        var property = await propertyRepository.GetByIdAsync(unit.PropertyId, cancellationToken);
        if (property is null || property.Status == PropertyStatus.Archived)
        {
            return Result.Failure(new Error("Property.Archived", "Cannot modify unit of an archived property."));
        }

        unit.SetStatus(request.NewStatus, clock.UtcNow);
        await unitRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}