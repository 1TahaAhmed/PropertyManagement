using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Tenants;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Tenants.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Tenants.Update;

public sealed class UpdateTenantCommandHandler(
    ITenantRepository tenantRepository,
    IClock clock) : IRequestHandler<UpdateTenantCommand, Result<TenantResponse>>
{
    public async Task<Result<TenantResponse>> Handle(
        UpdateTenantCommand request,
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

        if (tenant.Status == TenantStatus.Blacklisted)
        {
            return Result<TenantResponse>.Failure(new Error(
                Code: "Tenant.Blacklisted",
                Message: "Cannot update details of a blacklisted tenant.",
                Type: ErrorType.Validation));
        }

        var emailExists = await tenantRepository.ExistsByEmailAsync(
            request.OrganizationId,
            request.Email,
            excludeTenantId: tenant.Id,
            cancellationToken);

        if (emailExists)
        {
            return Result<TenantResponse>.Failure(new Error(
                Code: "Tenant.DuplicateEmail",
                Message: "Another tenant with this email already exists in the organization.",
                Type: ErrorType.Conflict));
        }

        if (!string.IsNullOrWhiteSpace(request.RowVersion))
        {
            try
            {
                var rowVersionBytes = Convert.FromBase64String(request.RowVersion);
                tenantRepository.SetRowVersion(tenant, rowVersionBytes);
            }
            catch (FormatException)
            {
                return Result<TenantResponse>.Failure(new Error(
                    Code: "Tenant.InvalidRowVersion",
                    Message: "The provided RowVersion concurrency token is invalid.",
                    Type: ErrorType.Validation));
            }
        }

        tenant.UpdateProfile(
            firstName: request.FirstName,
            lastName: request.LastName,
            email: request.Email,
            phoneNumber: request.PhoneNumber,
            nationalIdOrPassport: request.NationalIdOrPassport,
            emergencyContactName: request.EmergencyContactName,
            emergencyContactPhone: request.EmergencyContactPhone,
            notes: request.Notes,
            utcNow: clock.UtcNow);

        await tenantRepository.SaveChangesAsync(cancellationToken);

        return Result<TenantResponse>.Success(tenant.ToResponse());
    }
}
