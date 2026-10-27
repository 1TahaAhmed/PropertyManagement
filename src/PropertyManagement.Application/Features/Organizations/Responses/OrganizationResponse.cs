using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Organizations.Responses;

public sealed record OrganizationResponse(
    Guid Id,
    string Name,
    string Identifier,
    OrganizationStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);