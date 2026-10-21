using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Features.Properties.Response;

public sealed record PropertyResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string Address,
    string Description,
    PropertyType Type,
    PropertyStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string RowVersion);