using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Api.Contracts.Properties;

public sealed record CreatePropertyRequest(
    Guid OrganizationId,
    string Name,
    string Address,
    PropertyType Type,
    string Description = "");

public sealed record UpdatePropertyRequest(
    Guid OrganizationId,
    string Name,
    string Address,
    string Description,
    PropertyType Type,
    string RowVersion);
