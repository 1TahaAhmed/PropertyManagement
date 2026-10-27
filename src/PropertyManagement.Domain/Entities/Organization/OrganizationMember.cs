using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Organizations;

public sealed class OrganizationMember
{
    private OrganizationMember() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public MembershipRole Role { get; private set; }
    public MembershipStatus Status { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public Organization Organization { get; private set; } = null!;

    public static OrganizationMember Create(
        Guid organizationId,
        Guid userId,
        MembershipRole role,
        DateTime utcNow)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));

        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        return new OrganizationMember
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Role = role,
            Status = MembershipStatus.Active,
            JoinedAt = utcNow
        };
    }

    public void ChangeRole(MembershipRole newRole, DateTime utcNow)
    {
        Role = newRole;
        UpdatedAt = utcNow;
    }

    public void Deactivate(DateTime utcNow)
    {
        Status = MembershipStatus.Inactive;
        UpdatedAt = utcNow;
    }

    public void Activate(DateTime utcNow)
    {
        Status = MembershipStatus.Active;
        UpdatedAt = utcNow;
    }
}