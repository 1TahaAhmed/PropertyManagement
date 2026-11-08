using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Residents.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Residents.EmergencyContacts;

public sealed class UpdateEmergencyContactCommandHandler(
    IResidentRepository residentRepository,
    IClock clock)
    : IRequestHandler<UpdateEmergencyContactCommand, Result<EmergencyContactResponse>>
{
    public async Task<Result<EmergencyContactResponse>> Handle(
        UpdateEmergencyContactCommand request,
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
                Message: "Cannot update emergency contact for an archived resident.",
                Type: ErrorType.Validation));
        }

        try
        {
            var updatedContact = resident.UpdateEmergencyContact(
                contactId: request.ContactId,
                name: request.Name,
                relationship: request.Relationship,
                phone: request.Phone,
                utcNow: clock.UtcNow,
                email: request.Email,
                notes: request.Notes);

            await residentRepository.SaveChangesAsync(cancellationToken);

            return Result<EmergencyContactResponse>.Success(updatedContact.ToResponse());
        }
        catch (InvalidOperationException ex)
        {
            return Result<EmergencyContactResponse>.Failure(new Error(
                Code: "EmergencyContact.NotFound",
                Message: ex.Message,
                Type: ErrorType.NotFound));
        }
    }
}
