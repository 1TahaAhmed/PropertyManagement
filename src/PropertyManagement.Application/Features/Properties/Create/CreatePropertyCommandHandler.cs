using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Properties.Response;
using PropertyManagement.Domain.Entities.Properties;

namespace PropertyManagement.Application.Features.Properties.Create;

public sealed class CreatePropertyCommandHandler(
    IPropertyRepository propertyRepository,
    IClock clock)
    : IRequestHandler<CreatePropertyCommand, Result<PropertyResponse>>
{
    public async Task<Result<PropertyResponse>> Handle(CreatePropertyCommand request, CancellationToken cancellationToken)
    {
        var trimmedName = request.Name.Trim();

        var nameExists = await propertyRepository.ExistsByNameAsync(request.TenantId, trimmedName, request.excludePropertyId,cancellationToken);
        if (nameExists)
        {
            return Result<PropertyResponse>.Failure(new Error(
                Code: "Property.DuplicateName",
                Message: $"A property with the name '{trimmedName}' already exists.",
                Type: ErrorType.Conflict));
        }

        var property = Property.Create(
            request.TenantId,
            trimmedName,
            request.Address.Trim(),
            request.Type,
            clock.UtcNow,
            request.Description);

        await propertyRepository.AddAsync(property, cancellationToken);
        await propertyRepository.SaveChangesAsync(cancellationToken);

        return Result<PropertyResponse>.Success(new PropertyResponse(
            property.Id,
            property.TenantId,
            property.Name,
            property.Address,
            property.Description,
            property.Type,
            property.Status,
            property.CreatedAt,
            property.UpdatedAt,
            Convert.ToBase64String(property.RowVersion)));
    }
}