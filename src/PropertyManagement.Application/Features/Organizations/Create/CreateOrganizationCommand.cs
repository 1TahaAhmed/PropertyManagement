using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Organizations.Responses;
using PropertyManagement.Domain.Common;

namespace PropertyManagement.Application.Features.Organizations.Create;

public sealed record CreateOrganizationCommand(
    Guid UserId,
    string Name,
    string? CustomSlug = null,
    string DefaultCurrency = CurrencyCode.Default) : IRequest<Result<OrganizationResponse>>;
