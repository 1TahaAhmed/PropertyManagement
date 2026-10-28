using MediatR;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Email;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Companies.Register;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Authentication.RegisterCompany;

public sealed class RegisterCompanyCommandHandler(
    IIdentityService identityService,
    IOrganizationRepository organizationRepository,
    IEmailConfirmationCodeIssuer codeIssuer,
    IClock clock)
    : IRequestHandler<RegisterCompanyCommand, Result<RegisterCompanyResponse>>
{
    public async Task<Result<RegisterCompanyResponse>> Handle(
        RegisterCompanyCommand request,
        CancellationToken cancellationToken)
    {
        var trimmedCompanyName = request.CompanyName.Trim();

        var companyExists = await organizationRepository.ExistsByNameAsync(
            trimmedCompanyName,
            cancellationToken);

        if (companyExists)
        {
            return Result<RegisterCompanyResponse>.Failure(new Error(
                Code: "Organization.DuplicateName",
                Message: $"A company with the name '{trimmedCompanyName}' already exists.",
                Type: ErrorType.Conflict));
        }

        var identityResult = await identityService.RegisterAsync(
            request.FirstName,
            request.LastName,
            request.Address,
            request.Email,
            request.Password,
            cancellationToken);

        if (identityResult.IsFailure)
        {
            return Result<RegisterCompanyResponse>.Failure(
                identityResult.Errors.ToArray());
        }

        var identityUser = identityResult.Value;
        var utcNow = clock.UtcNow;

        var baseSlug = Organization.GenerateSlug(trimmedCompanyName);
        var uniqueSlug = baseSlug;
        var suffix = 1;

        while (await organizationRepository.ExistsByIdentifierAsync(uniqueSlug, cancellationToken))
        {
            suffix++;
            uniqueSlug = $"{baseSlug}-{suffix}";
        }

        var organization = Organization.Create(trimmedCompanyName, utcNow, customSlug: uniqueSlug);

        var ownerMember = OrganizationMember.Create(
            organization.Id,
            identityUser.UserId,
            MembershipRole.Owner,
            utcNow);

        await organizationRepository.AddAsync(organization, cancellationToken);
        await organizationRepository.AddMemberAsync(ownerMember, cancellationToken);
        await organizationRepository.SaveChangesAsync(cancellationToken);

        var codeIssueResult = await codeIssuer.IssueAsync(
            userId: identityUser.UserId,
            email: request.Email,
            firstName: request.FirstName,
            cancellationToken: cancellationToken);

        if (codeIssueResult.IsFailure)
        {
            return Result<RegisterCompanyResponse>.Failure(
                codeIssueResult.Errors.ToArray());
        }

        var response = new RegisterCompanyResponse(
            UserId: identityUser.UserId,
            Email: request.Email,
            FirstName: request.FirstName,
            LastName: request.LastName,
            OrganizationId: organization.Id,
            OrganizationName: organization.Name,
            RequiresEmailConfirmation: true
        );

        return Result<RegisterCompanyResponse>.Success(response);
    }
}