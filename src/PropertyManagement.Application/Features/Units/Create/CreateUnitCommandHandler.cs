using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Units.Response;
using PropertyManagement.Domain.Enums;
using Unit = PropertyManagement.Domain.Entities.Properties.Unit;

namespace PropertyManagement.Application.Features.Units.Create;

public sealed class CreateUnitCommandHandler(
    IPropertyRepository propertyRepository,
    IUnitRepository unitRepository,
    IClock clock)
    : IRequestHandler<CreateUnitCommand, Result<UnitResponse>>
{
    public async Task<Result<UnitResponse>> Handle(CreateUnitCommand request, CancellationToken cancellationToken)
    {
        var property = await propertyRepository.GetByIdAsync(request.PropertyId, cancellationToken);

        // Prevent Information Disclosure: return NotFound if property belongs to another tenant
        if (property is null || property.TenantId != request.TenantId)
        {
            return Result<UnitResponse>.Failure(new Error(
                Code: "Property.NotFound",
                Message: "The specified property does not exist.",
                Type: ErrorType.NotFound));
        }

        if (property.Status == PropertyStatus.Archived)
        {
            return Result<UnitResponse>.Failure(new Error(
                Code: "Property.Archived",
                Message: "Cannot add units to an archived property.",
                Type: ErrorType.Validation));
        }

        var trimmedUnitNumber = request.UnitNumber.Trim();

        var unitExists = await unitRepository.ExistsByNumberAsync(
            request.TenantId,
            request.PropertyId,
            trimmedUnitNumber,
            cancellationToken);

        if (unitExists)
        {
            return Result<UnitResponse>.Failure(new Error(
                Code: "Unit.DuplicateNumber",
                Message: $"A unit with number '{trimmedUnitNumber}' already exists in this property.",
                Type: ErrorType.Conflict));
        }

        var unit = Unit.Create(
            request.TenantId,
            request.PropertyId,
            trimmedUnitNumber,
            request.Type,
            request.MarketRent,
            clock.UtcNow,
            request.FloorNumber,
            request.Bedrooms,
            request.Bathrooms,
            request.SquareMeters,
            request.Description);

        await unitRepository.AddAsync(unit, cancellationToken);
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