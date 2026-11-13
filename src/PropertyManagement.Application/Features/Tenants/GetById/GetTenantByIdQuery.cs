using MediatR;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Tenants.Responses;

namespace PropertyManagement.Application.Features.Tenants.GetById;

public sealed record GetTenantByIdQuery(
    Guid TenantId,
    Guid OrganizationId) : IRequest<Result<TenantResponse>>;
