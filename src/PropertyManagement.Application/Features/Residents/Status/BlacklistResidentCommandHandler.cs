using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Residents.Status;

public sealed class BlacklistResidentCommandHandler(
    IResidentRepository residentRepository,
    IClock clock) : IRequestHandler<BlacklistResidentCommand, Result>
{
    public async Task<Result> Handle(BlacklistResidentCommand request, CancellationToken cancellationToken)
    {
        var resident = await residentRepository.GetByIdAsync(request.ResidentId, request.OrganizationId, cancellationToken);
        if (resident is null || resident.OrganizationId != request.OrganizationId)
        {
            return Result.Failure(new Error("Resident.NotFound", "Resident was not found.", Type: ErrorType.NotFound));
        }

        if (resident.Status == ResidentStatus.Archived)
        {
            return Result.Failure(new Error("Resident.Archived", "Cannot blacklist an archived resident.", Type: ErrorType.Validation));
        }

        resident.Blacklist(request.Reason, clock.UtcNow);
        await residentRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
