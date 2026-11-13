using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Tenants;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Tenants.Status;

public sealed class DeactivateTenantCommandHandler(
    ITenantRepository tenantRepository,
    IClock clock) : IRequestHandler<DeactivateTenantCommand, Result>
{
    public async Task<Result> Handle(
        DeactivateTenantCommand request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(
            request.TenantId,
            request.OrganizationId,
            cancellationToken);

        if (tenant is null || tenant.OrganizationId != request.OrganizationId)
        {
            return Result.Failure(new Error(
                Code: "Tenant.NotFound",
                Message: "Tenant was not found.",
                Type: ErrorType.NotFound));
        }

        tenant.Deactivate(clock.UtcNow);
        await tenantRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
