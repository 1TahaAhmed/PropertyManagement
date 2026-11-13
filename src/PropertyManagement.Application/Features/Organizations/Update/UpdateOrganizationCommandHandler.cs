using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Organizations.Responses;

namespace PropertyManagement.Application.Features.Organizations.Update;

public sealed class UpdateOrganizationCommandHandler(
    IOrganizationRepository organizationRepository,
    IClock clock) : IRequestHandler<UpdateOrganizationCommand, Result<OrganizationResponse>>
{
    public async Task<Result<OrganizationResponse>> Handle(
        UpdateOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        var organization = await organizationRepository.GetByIdAsync(command.OrganizationId, cancellationToken);
        if (organization is null)
        {
            return Result<OrganizationResponse>.Failure(new Error(
                Code: "Organization.NotFound",
                Message: "Organization was not found.",
                Type: ErrorType.NotFound));
        }

        var trimmedName = command.Name.Trim();
        if (!string.Equals(organization.Name, trimmedName, StringComparison.OrdinalIgnoreCase) &&
            await organizationRepository.ExistsByNameAsync(trimmedName, cancellationToken))
        {
            return Result<OrganizationResponse>.Failure(new Error(
                Code: "Organization.DuplicateName",
                Message: $"An organization with the name '{trimmedName}' already exists.",
                Type: ErrorType.Conflict));
        }

        organization.UpdateName(trimmedName, clock.UtcNow);
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
