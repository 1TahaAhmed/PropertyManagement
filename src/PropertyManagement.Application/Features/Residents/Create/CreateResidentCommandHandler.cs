using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Residents.Responses;
using PropertyManagement.Domain.Entities.Residents;

namespace PropertyManagement.Application.Features.Residents.Create;

public sealed class CreateResidentCommandHandler(
    IResidentRepository residentRepository,
    IClock clock)
    : IRequestHandler<CreateResidentCommand, Result<ResidentResponse>>
{
    public async Task<Result<ResidentResponse>> Handle(
        CreateResidentCommand request,
        CancellationToken cancellationToken)
    {
        var resident = Resident.Create(
            organizationId: request.OrganizationId,
            firstName: request.FirstName,
            lastName: request.LastName,
            phone: request.Phone,
            utcNow: clock.UtcNow,
            middleName: request.MiddleName,
            preferredName: request.PreferredName,
            email: request.Email,
            alternatePhone: request.AlternatePhone,
            nationalIdOrPassport: request.NationalIdOrPassport ?? string.Empty,
            notes: request.Notes ?? string.Empty);

        await residentRepository.AddAsync(resident, cancellationToken);
        await residentRepository.SaveChangesAsync(cancellationToken);

        return Result<ResidentResponse>.Success(resident.ToResponse());
    }
}
