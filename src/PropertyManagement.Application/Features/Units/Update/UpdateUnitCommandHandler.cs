using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Units.Response;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Units.Update;

public sealed class UpdateUnitCommandHandler(
    IPropertyRepository propertyRepository,
    IUnitRepository unitRepository,
    IClock clock)
    : IRequestHandler<UpdateUnitCommand, Result<UnitResponse>>
{
    public async Task<Result<UnitResponse>> Handle(UpdateUnitCommand request, CancellationToken cancellationToken)
    {
        var unit = await unitRepository.GetByIdAsync(request.UnitId, cancellationToken);
        if (unit is null || unit.TenantId != request.TenantId)
        {
            return Result<UnitResponse>.Failure(new Error(
                Code: "Unit.NotFound",
                Message: "Unit was not found.",
                Type: ErrorType.NotFound));
        }

        var property = await propertyRepository.GetByIdAsync(unit.PropertyId, cancellationToken);

        if (property is null || property.Status == PropertyStatus.Archived)
        {
            return Result<UnitResponse>.Failure(new Error(
                Code: "Property.Archived",
                Message: "Cannot modify units of an archived property.",
                Type: ErrorType.Validation));
        }

        var trimmedUnitNumber = request.UnitNumber.Trim();

        if (!string.Equals(unit.UnitNumber, trimmedUnitNumber, StringComparison.OrdinalIgnoreCase))
        {
            var numberTaken = await unitRepository.ExistsByNumberAsync(
                unit.TenantId,
                unit.PropertyId,
                trimmedUnitNumber,
                cancellationToken);

            if (numberTaken)
            {
                return Result<UnitResponse>.Failure(new Error(
                    Code: "Unit.DuplicateNumber",
                    Message: $"A unit with number '{trimmedUnitNumber}' already exists in this property.",
                    Type: ErrorType.Conflict));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.RowVersion))
        {
            try
            {
                var rowVersionBytes = Convert.FromBase64String(request.RowVersion);
                unitRepository.SetRowVersion(unit, rowVersionBytes);
            }
            catch (FormatException)
            {
                return Result<UnitResponse>.Failure(new Error(
                    Code: "Unit.InvalidRowVersion",
                    Message: "The provided RowVersion concurrency token is invalid.",
                    Type: ErrorType.Validation));
            }
        }

        unit.UpdateDetails(
            trimmedUnitNumber,
            request.Type,
            request.MarketRent,
            request.FloorNumber,
            request.Bedrooms,
            request.Bathrooms,
            request.SquareMeters,
            request.Description,
            clock.UtcNow);

        await unitRepository.SaveChangesAsync(cancellationToken);

        return Result<UnitResponse>.Success(new UnitResponse(
            unit.Id,
            unit.TenantId,
            unit.PropertyId,
            unit.UnitNumber,
            unit.FloorNumber,
            unit.Type,
            unit.Status,
            unit.MarketRent,
            unit.Bedrooms,
            unit.Bathrooms,
            unit.SquareMeters,
            unit.Description,
            unit.CreatedAt,
            unit.UpdatedAt,
            Convert.ToBase64String(unit.RowVersion)));
    }
}