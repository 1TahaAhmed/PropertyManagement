using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Residents.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Residents.Update;

public sealed class UpdateResidentCommandHandler(
    IResidentRepository residentRepository,
    IClock clock)
    : IRequestHandler<UpdateResidentCommand, Result<ResidentResponse>>
{
    public async Task<Result<ResidentResponse>> Handle(
        UpdateResidentCommand request,
        CancellationToken cancellationToken)
    {
        var resident = await residentRepository.GetByIdWithContactsAsync(
            request.ResidentId,
            request.OrganizationId,
            cancellationToken);

        if (resident is null || resident.OrganizationId != request.OrganizationId)
        {
            return Result<ResidentResponse>.Failure(new Error(
                Code: "Resident.NotFound",
                Message: "Resident was not found.",
                Type: ErrorType.NotFound));
        }

        if (resident.Status == ResidentStatus.Archived)
        {
            return Result<ResidentResponse>.Failure(new Error(
                Code: "Resident.Archived",
                Message: "Cannot update an archived resident.",
                Type: ErrorType.Validation));
        }

        if (!string.IsNullOrWhiteSpace(request.RowVersion))
        {
            try
            {
                var rowVersionBytes = Convert.FromBase64String(request.RowVersion);
                residentRepository.SetRowVersion(resident, rowVersionBytes);
            }
            catch (FormatException)
            {
                return Result<ResidentResponse>.Failure(new Error(
                    Code: "Resident.InvalidRowVersion",
                    Message: "The provided RowVersion concurrency token is invalid.",
                    Type: ErrorType.Validation));
            }
        }

        resident.UpdateDetails(
            firstName: request.FirstName,
            lastName: request.LastName,
            phone: request.Phone,
            utcNow: clock.UtcNow,
            middleName: request.MiddleName,
            preferredName: request.PreferredName,
            email: request.Email,
            alternatePhone: request.AlternatePhone);

        await residentRepository.SaveChangesAsync(cancellationToken);

        return Result<ResidentResponse>.Success(resident.ToResponse());
    }
}
