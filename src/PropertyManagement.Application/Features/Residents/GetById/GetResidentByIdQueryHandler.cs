using MediatR;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Residents.Responses;

namespace PropertyManagement.Application.Features.Residents.GetById;

public sealed class GetResidentByIdQueryHandler(
    IResidentRepository residentRepository)
    : IRequestHandler<GetResidentByIdQuery, Result<ResidentResponse>>
{
    public async Task<Result<ResidentResponse>> Handle(
        GetResidentByIdQuery request,
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

        return Result<ResidentResponse>.Success(resident.ToResponse());
    }
}
