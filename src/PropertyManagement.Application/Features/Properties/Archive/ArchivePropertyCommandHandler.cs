using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Properties;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Properties.Archive;

public sealed class ArchivePropertyCommandHandler(
    IPropertyRepository propertyRepository,
    IClock clock)
    : IRequestHandler<ArchivePropertyCommand, Result>
{
    public async Task<Result> Handle(ArchivePropertyCommand request, CancellationToken cancellationToken)
    {
        var property = await propertyRepository.GetByIdAsync(request.PropertyId, cancellationToken);
        if (property is null)
        {
            return Result.Failure(new Error(
                Code: "Property.NotFound",
                Message: "Property was not found.",
                Type: ErrorType.NotFound));
        }

        property.Archive(clock.UtcNow);
        await propertyRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}