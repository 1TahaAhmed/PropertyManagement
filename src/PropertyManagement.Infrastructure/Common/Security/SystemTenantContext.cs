using PropertyManagement.Application.Abstractions.Security;

namespace PropertyManagement.Infrastructure.Common.Security;

public sealed class SystemTenantContext : ISystemTenantContext
{
    private readonly AsyncLocal<Guid?> _scopedOrganizationId = new();

    public Guid? ScopedOrganizationId => _scopedOrganizationId.Value;

    public IDisposable BeginScope(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));
        }

        var previous = _scopedOrganizationId.Value;
        _scopedOrganizationId.Value = organizationId;
        return new ScopeHandle(this, previous);
    }

    private sealed class ScopeHandle(SystemTenantContext owner, Guid? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            owner._scopedOrganizationId.Value = previous;
        }
    }
}
