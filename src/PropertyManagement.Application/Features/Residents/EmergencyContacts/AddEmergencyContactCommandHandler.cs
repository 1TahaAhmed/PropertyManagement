using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Residents.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Residents.EmergencyContacts;

public sealed class AddEmergencyContactCommandHandler(
    IResidentRepository residentRepository,
    IClock clock)
    : IRequestHandler<AddEmergencyContactCommand, Result<EmergencyContactResponse>>
{
    public async Task<Result<EmergencyContactResponse>> Handle(
        AddEmergencyContactCommand request,
        CancellationToken cancellationToken)
    {
        var resident = await residentRepository.GetByIdWithContactsAsync(
            request.ResidentId,
            request.OrganizationId,
            cancellationToken);

        if (resident is null || resident.OrganizationId != request.OrganizationId)
        {
            return Result<EmergencyContactResponse>.Failure(new Error(
                Code: "Resident.NotFound",
                Message: "Resident was not found.",
                Type: ErrorType.NotFound));
        }

        if (resident.Status == ResidentStatus.Archived)
        {
            return Result<EmergencyContactResponse>.Failure(new Error(
                Code: "Resident.Archived",
                Message: "Cannot add emergency contact to an archived resident.",
                Type: ErrorType.Validation));
        }

        var contact = resident.AddEmergencyContact(
            name: request.Name,
            relationship: request.Relationship,
            phone: request.Phone,
            utcNow: clock.UtcNow,
            email: request.Email,
            notes: request.Notes);

        await residentRepository.SaveChangesAsync(cancellationToken);

        return Result<EmergencyContactResponse>.Success(contact.ToResponse());
    }
}
