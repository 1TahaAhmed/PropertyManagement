using MediatR;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Properties.Archive
{
    public sealed record ArchivePropertyCommand(
        Guid PropertyId,
        Guid OrganizationId) 
        : IRequest<Result>;
}
