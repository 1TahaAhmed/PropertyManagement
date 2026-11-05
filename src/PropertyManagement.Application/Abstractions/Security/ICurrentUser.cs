namespace PropertyManagement.Application.Abstractions.Security;

public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? OrganizationId { get; }
    string? Role { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
}
