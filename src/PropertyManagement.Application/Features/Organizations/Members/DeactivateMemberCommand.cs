using MediatR;
using PropertyManagement.Application.Common.Results;

namespace PropertyManagement.Application.Features.Organizations.Members;

public sealed record DeactivateMemberCommand(
    Guid OrganizationId,
    Guid MemberId) : IRequest<Result>;
