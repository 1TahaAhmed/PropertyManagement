using MediatR;
using PropertyManagement.Application.Common.Models;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Residents.Responses;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Residents.GetResidents;

public sealed record GetResidentsQuery(
    Guid OrganizationId,
    string? SearchTerm = null,
    ResidentStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 10,
    LeaseResidentRole? LeaseRole = null,
    bool OnlyLeaseHolders = false) : IRequest<Result<PagedList<ResidentSummaryResponse>>>;
