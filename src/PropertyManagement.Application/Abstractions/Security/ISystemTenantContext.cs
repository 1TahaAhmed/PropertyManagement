namespace PropertyManagement.Application.Abstractions.Security;

public interface ISystemTenantContext
{
    Guid? ScopedOrganizationId { get; }
    IDisposable BeginScope(Guid organizationId);
}
