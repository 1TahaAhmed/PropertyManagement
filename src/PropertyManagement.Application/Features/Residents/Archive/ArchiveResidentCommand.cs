using MediatR;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Residents.Archive;

public sealed record ArchiveResidentCommand(
    Guid ResidentId,
    Guid OrganizationId) : IRequest<Result>;
