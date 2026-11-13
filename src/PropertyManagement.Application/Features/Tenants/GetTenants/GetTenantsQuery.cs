using MediatR;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Tenants.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Tenants.GetTenants;

public sealed record GetTenantsQuery(
    Guid OrganizationId,
    string? SearchTerm = null,
    TenantStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<Result<PagedList<TenantResponse>>>;
