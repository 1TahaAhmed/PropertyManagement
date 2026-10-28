namespace PropertyManagement.Domain.Common;

public interface ITenantScoped
{
    Guid OrganizationId { get; }
}