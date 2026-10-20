using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Properties.Response;
using PropertyManagement.Application.Features.Properties.Update;
using PropertyManagement.Domain.Enums;

public sealed class UpdatePropertyCommandHandler(
    IPropertyRepository propertyRepository,
    IClock clock)
    : IRequestHandler<UpdatePropertyCommand, Result<PropertyResponse>>
{
    public async Task<Result<PropertyResponse>> Handle(UpdatePropertyCommand request, CancellationToken cancellationToken)
    {
        var property = await propertyRepository.GetByIdAsync(request.PropertyId, cancellationToken);
        if (property is null)
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

        property.UpdateDetails(request.Name, request.Address, request.Description, request.Type, clock.UtcNow);
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
            property.UpdatedAt));
    }
}
