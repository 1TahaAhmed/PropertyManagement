using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Tenants;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Tenants.Responses;
using PropertyManagement.Domain.Entities.Tenants;

namespace PropertyManagement.Application.Features.Tenants.Create;

public sealed class CreateTenantCommandHandler(
    ITenantRepository tenantRepository,
    IClock clock) : IRequestHandler<CreateTenantCommand, Result<TenantResponse>>
{
    public async Task<Result<TenantResponse>> Handle(
        CreateTenantCommand request,
        CancellationToken cancellationToken)
    {
        var emailExists = await tenantRepository.ExistsByEmailAsync(
            request.OrganizationId,
            request.Email,
            excludeTenantId: null,
            cancellationToken);

        if (emailExists)
        {
            return Result<TenantResponse>.Failure(new Error(
                Code: "Tenant.DuplicateEmail",
                Message: "A tenant with this email already exists in the organization.",
                Type: ErrorType.Conflict));
        }

        var tenant = Tenant.Create(
            organizationId: request.OrganizationId,
            firstName: request.FirstName,
            lastName: request.LastName,
            email: request.Email,
            phoneNumber: request.PhoneNumber,
            utcNow: clock.UtcNow,
            nationalIdOrPassport: request.NationalIdOrPassport,
            emergencyContactName: request.EmergencyContactName,
            emergencyContactPhone: request.EmergencyContactPhone,
            notes: request.Notes);

        await tenantRepository.AddAsync(tenant, cancellationToken);
        await tenantRepository.SaveChangesAsync(cancellationToken);

        return Result<TenantResponse>.Success(tenant.ToResponse());
    }
}
