using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Organizations.Responses;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Organizations.Create;

public sealed class CreateOrganizationCommandHandler(
    IOrganizationRepository organizationRepository,
    IClock clock) : IRequestHandler<CreateOrganizationCommand, Result<OrganizationResponse>>
{
    public async Task<Result<OrganizationResponse>> Handle(
        CreateOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        var trimmedName = command.Name.Trim();
        if (await organizationRepository.ExistsByNameAsync(trimmedName, cancellationToken))
        {
            return Result<OrganizationResponse>.Failure(new Error(
                Code: "Organization.DuplicateName",
                Message: $"An organization with the name '{trimmedName}' already exists.",
                Type: ErrorType.Conflict));
        }

        string uniqueSlug;
        if (!string.IsNullOrWhiteSpace(command.CustomSlug))
        {
            uniqueSlug = command.CustomSlug.Trim().ToLowerInvariant();
            if (await organizationRepository.ExistsByIdentifierAsync(uniqueSlug, cancellationToken))
            {
                return Result<OrganizationResponse>.Failure(new Error(
                    Code: "Organization.DuplicateIdentifier",
                    Message: $"An organization with identifier '{uniqueSlug}' already exists.",
                    Type: ErrorType.Conflict));
            }
        }
        else
        {
            var baseSlug = Organization.GenerateSlug(trimmedName);
            uniqueSlug = baseSlug;
            var suffix = 1;
            while (await organizationRepository.ExistsByIdentifierAsync(uniqueSlug, cancellationToken))
            {
                suffix++;
                uniqueSlug = $"{baseSlug}-{suffix}";
            }
        }

        var utcNow = clock.UtcNow;
        var organization = Organization.Create(trimmedName, utcNow, customSlug: uniqueSlug);
        var ownerMember = OrganizationMember.Create(
            organization.Id,
            command.UserId,
            MembershipRole.Owner,
            utcNow);

        await organizationRepository.AddAsync(organization, cancellationToken);
        await organizationRepository.AddMemberAsync(ownerMember, cancellationToken);
        await organizationRepository.SaveChangesAsync(cancellationToken);

        return Result<OrganizationResponse>.Success(new OrganizationResponse(
            Id: organization.Id,
            Name: organization.Name,
            Identifier: organization.Identifier,
            Status: organization.Status,
            CreatedAt: organization.CreatedAt,
            UpdatedAt: organization.UpdatedAt));
    }
}
