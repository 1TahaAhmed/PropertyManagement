using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Properties.Create;
using PropertyManagement.Application.Features.Properties.Response;
using PropertyManagement.Domain.Entities.Properties;

public sealed class CreatePropertyCommandHandler(
    IPropertyRepository propertyRepository,
    IClock clock)
    : IRequestHandler<CreatePropertyCommand, Result<PropertyResponse>>
{
    public async Task<Result<PropertyResponse>> Handle(CreatePropertyCommand request, CancellationToken cancellationToken)
    {
        var nameExists = await propertyRepository.ExistsByNameAsync(request.TenantId, request.Name, cancellationToken);
        if (nameExists)
        {
            return Result<PropertyResponse>.Failure(new Error(
                Code: "Property.DuplicateName",
                Message: $"A property with the name '{request.Name}' already exists.",
                Type: ErrorType.Conflict));
        }

        var property = Property.Create(
            request.TenantId,
            request.Name,
            request.Address,
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
            property.UpdatedAt));
    }
}