using MediatR;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Properties.Response;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Properties.GetProperties;

public sealed record GetPropertiesQuery(
    Guid OrganizationId,
    string? SearchTerm = null,
    PropertyStatus? Status = null,
    PropertyType? Type = null,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<Result<PagedList<PropertyResponse>>>;
