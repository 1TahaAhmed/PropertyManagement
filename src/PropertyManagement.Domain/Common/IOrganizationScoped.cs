namespace PropertyManagement.Domain.Common;

public interface IOrganizationScoped
{
    Guid OrganizationId { get; }
}
