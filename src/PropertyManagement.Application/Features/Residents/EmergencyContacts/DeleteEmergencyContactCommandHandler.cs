using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Residents.EmergencyContacts;

public sealed class DeleteEmergencyContactCommandHandler(
    IResidentRepository residentRepository,
    IClock clock)
    : IRequestHandler<DeleteEmergencyContactCommand, Result>
{
    public async Task<Result> Handle(
        DeleteEmergencyContactCommand request,
        CancellationToken cancellationToken)
    {
        var resident = await residentRepository.GetByIdWithContactsAsync(
            request.ResidentId,
            request.OrganizationId,
            cancellationToken);

        if (resident is null || resident.OrganizationId != request.OrganizationId)
        {
            return Result.Failure(new Error(
                Code: "Resident.NotFound",
                Message: "Resident was not found.",
                Type: ErrorType.NotFound));
        }

        if (resident.Status == ResidentStatus.Archived)
        {
            return Result.Failure(new Error(
                Code: "Resident.Archived",
                Message: "Cannot delete emergency contact of an archived resident.",
                Type: ErrorType.Validation));
        }

        var contact = resident.EmergencyContacts.FirstOrDefault(c => c.Id == request.ContactId);
        if (contact is null)
        {
            return Result.Failure(new Error(
                Code: "EmergencyContact.NotFound",
                Message: $"Emergency contact '{request.ContactId}' was not found.",
                Type: ErrorType.NotFound));
        }

        resident.RemoveEmergencyContact(request.ContactId, clock.UtcNow);
        residentRepository.RemoveEmergencyContact(contact);
        await residentRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
