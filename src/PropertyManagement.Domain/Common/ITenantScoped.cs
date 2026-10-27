namespace PropertyManagement.Domain.Common;

public interface ITenantScoped
{
    Guid TenantId { get; }
}