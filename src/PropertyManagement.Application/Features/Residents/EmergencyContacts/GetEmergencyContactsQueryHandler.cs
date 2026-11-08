using MediatR;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Residents.Responses;

namespace PropertyManagement.Application.Features.Residents.EmergencyContacts;

public sealed class GetEmergencyContactsQueryHandler(
    IResidentRepository residentRepository)
    : IRequestHandler<GetEmergencyContactsQuery, Result<IReadOnlyList<EmergencyContactResponse>>>
{
    public async Task<Result<IReadOnlyList<EmergencyContactResponse>>> Handle(
        GetEmergencyContactsQuery request,
        CancellationToken cancellationToken)
    {
        var resident = await residentRepository.GetByIdWithContactsAsync(
            request.ResidentId,
            request.OrganizationId,
            cancellationToken);

        if (resident is null || resident.OrganizationId != request.OrganizationId)
        {
            return Result<IReadOnlyList<EmergencyContactResponse>>.Failure(new Error(
                Code: "Resident.NotFound",
                Message: "Resident was not found.",
                Type: ErrorType.NotFound));
        }

        var responses = resident.EmergencyContacts.Select(c => c.ToResponse()).ToList();

        return Result<IReadOnlyList<EmergencyContactResponse>>.Success(responses);
    }
}
