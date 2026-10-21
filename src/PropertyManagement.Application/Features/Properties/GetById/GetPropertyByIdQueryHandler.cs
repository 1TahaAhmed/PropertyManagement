using MediatR;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Properties.Response;

namespace PropertyManagement.Application.Features.Properties.GetById;

public sealed class GetPropertyByIdQueryHandler(IPropertyRepository propertyRepository)
    : IRequestHandler<GetPropertyByIdQuery, Result<PropertyResponse>>
{
    public async Task<Result<PropertyResponse>> Handle(GetPropertyByIdQuery request, CancellationToken cancellationToken)
    {
        var property = await propertyRepository.GetByIdAsync(request.PropertyId, cancellationToken);
        if (property is null)
        {
            return Result<PropertyResponse>.Failure(new Error(
                Code: "Property.NotFound",
                Message: "Property was not found.",
                Type: ErrorType.NotFound));
        }

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
