using MediatR;
using PropertyManagement.Application.Abstractions.Tenants;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Tenants.Responses;

namespace PropertyManagement.Application.Features.Tenants.GetById;

public sealed class GetTenantByIdQueryHandler(
    ITenantRepository tenantRepository) : IRequestHandler<GetTenantByIdQuery, Result<TenantResponse>>
{
    public async Task<Result<TenantResponse>> Handle(
        GetTenantByIdQuery request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantRepository.GetByIdAsync(
            request.TenantId,
            request.OrganizationId,
            cancellationToken);

        if (tenant is null || tenant.OrganizationId != request.OrganizationId)
        {
            return Result<TenantResponse>.Failure(new Error(
                Code: "Tenant.NotFound",
                Message: "Tenant was not found.",
                Type: ErrorType.NotFound));
        }

        return Result<TenantResponse>.Success(tenant.ToResponse());
    }
}
