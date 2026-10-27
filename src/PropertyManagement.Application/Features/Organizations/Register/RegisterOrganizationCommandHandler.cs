using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Organizations.Responses;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Organizations.Register;

public sealed class RegisterOrganizationCommandHandler(
    IOrganizationRepository organizationRepository,
    IIdentityService identityService,
    IClock clock)
    : IRequestHandler<RegisterOrganizationCommand, Result<OrganizationResponse>>
{
    public async Task<Result<OrganizationResponse>> Handle(
        RegisterOrganizationCommand request,
        CancellationToken cancellationToken)
    {
        var trimmedName = request.Name.Trim();

        var userExists = await identityService.ExistsAsync(request.OwnerUserId, cancellationToken);
        if (!userExists)
        {
            return Result<OrganizationResponse>.Failure(new Error(
                Code: "User.NotFound",
                Message: "The specified owner user does not exist.",
                Type: ErrorType.NotFound));
        }

        var nameExists = await organizationRepository.ExistsByNameAsync(trimmedName, cancellationToken);
        if (nameExists)
        {
            return Result<OrganizationResponse>.Failure(new Error(
                Code: "Organization.DuplicateName",
                Message: $"An organization with the name '{trimmedName}' already exists.",
                Type: ErrorType.Conflict));
        }

        var utcNow = clock.UtcNow;

        var organization = Organization.Create(trimmedName, utcNow);

        var identifierExists = await organizationRepository.ExistsByIdentifierAsync(organization.Identifier, cancellationToken);
        if (identifierExists)
        {
            return Result<OrganizationResponse>.Failure(new Error(
                Code: "Organization.DuplicateIdentifier",
                Message: $"An organization with identifier '{organization.Identifier}' already exists.",
                Type: ErrorType.Conflict));
        }

        var ownerMember = OrganizationMember.Create(
            organization.Id,
            request.OwnerUserId,
            MembershipRole.Owner,
            utcNow);

        await organizationRepository.AddAsync(organization, cancellationToken);
        await organizationRepository.AddMemberAsync(ownerMember, cancellationToken);
        await organizationRepository.SaveChangesAsync(cancellationToken);

        return Result<OrganizationResponse>.Success(new OrganizationResponse(
            organization.Id,
            organization.Name,
            organization.Identifier,
            organization.Status,
            organization.CreatedAt,
            organization.UpdatedAt));
    }
}