using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Properties.Response;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Properties.Update;

public sealed class UpdatePropertyCommandHandler(
    IPropertyRepository propertyRepository,
    IClock clock)
    : IRequestHandler<UpdatePropertyCommand, Result<PropertyResponse>>
{
    public async Task<Result<PropertyResponse>> Handle(UpdatePropertyCommand request, CancellationToken cancellationToken)
    {
        var property = await propertyRepository.GetByIdAsync(request.PropertyId, cancellationToken);
        
        if (property is null || property.OrganizationId != request.OrganizationId)
        {
            return Result<PropertyResponse>.Failure(new Error(
                Code: "Property.NotFound",
                Message: "Property was not found.",
                Type: ErrorType.NotFound));
        }

        if (property.Status == PropertyStatus.Archived)
        {
            return Result<PropertyResponse>.Failure(new Error(
                Code: "Property.Archived",
                Message: "Cannot update an archived property.",
                Type: ErrorType.Validation));
        }

        var trimmedName = request.Name.Trim();
        if (!string.Equals(property.Name, trimmedName, StringComparison.OrdinalIgnoreCase))
        {
            var nameExists = await propertyRepository.ExistsByNameAsync(property.OrganizationId, trimmedName, excludePropertyId: request.PropertyId,cancellationToken);
            if (nameExists)
            {
                return Result<PropertyResponse>.Failure(new Error(
                    Code: "Property.DuplicateName",
                    Message: $"A property with the name '{trimmedName}' already exists.",
                    Type: ErrorType.Conflict));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.RowVersion))
        {
            try
            {
                var rowVersionBytes = Convert.FromBase64String(request.RowVersion);
                propertyRepository.SetRowVersion(property, rowVersionBytes);
            }
            catch (FormatException)
            {
                return Result<PropertyResponse>.Failure(new Error(
                    Code: "Property.InvalidRowVersion",
                    Message: "The provided RowVersion concurrency token is invalid.",
                    Type: ErrorType.Validation));
            }
        }

        property.UpdateDetails(trimmedName, request.Address.Trim(), request.Description, request.Type, clock.UtcNow);
        await propertyRepository.SaveChangesAsync(cancellationToken);

        return Result<PropertyResponse>.Success(new PropertyResponse(
            property.Id,
            property.OrganizationId,
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
