using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Residents.Archive;

public sealed class ArchiveResidentCommandHandler(
    IResidentRepository residentRepository,
    IClock clock)
    : IRequestHandler<ArchiveResidentCommand, Result>
{
    public async Task<Result> Handle(
        ArchiveResidentCommand request,
        CancellationToken cancellationToken)
    {
        var resident = await residentRepository.GetByIdAsync(
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

        resident.Archive(clock.UtcNow);
        await residentRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
