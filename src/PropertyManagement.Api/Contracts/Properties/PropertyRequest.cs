using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Contracts.Properties;

public sealed record CreatePropertyRequest(
    string Name,
    string Address,
    PropertyType Type,
    string Description = "",
    Guid OrganizationId = default);

public sealed record UpdatePropertyRequest(
    string Name,
    string Address,
    string Description,
    PropertyType Type,
    string RowVersion,
    Guid OrganizationId = default);
