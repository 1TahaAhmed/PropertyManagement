using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities.Organizations;

public sealed class Invitation
{
    private Invitation() { }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public MembershipRole Role { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public InvitationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RespondedAtUtc { get; private set; }

    public static Invitation Create(
        Guid organizationId,
        string email,
        MembershipRole role,
        string tokenHash,
        DateTime expiresAtUtc,
        DateTime utcNow)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("Organization ID cannot be empty.", nameof(organizationId));

        ArgumentException.ThrowIfNullOrWhiteSpace(email, nameof(email));
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash, nameof(tokenHash));

        if (expiresAtUtc <= utcNow)
            throw new ArgumentException("Expiration date must be in the future.", nameof(expiresAtUtc));

        return new Invitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Email = email.Trim().ToLowerInvariant(),
            Role = role,
            TokenHash = tokenHash,
            ExpiresAtUtc = expiresAtUtc,
            Status = InvitationStatus.Pending,
            CreatedAtUtc = utcNow
        };
    }

    public void Accept(DateTime utcNow)
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException("Only pending invitations can be accepted.");

        if (utcNow > ExpiresAtUtc)
        {
            Status = InvitationStatus.Expired;
            throw new InvalidOperationException("Invitation has expired.");
        }

        Status = InvitationStatus.Accepted;
        RespondedAtUtc = utcNow;
    }

    public void Revoke(DateTime utcNow)
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException("Only pending invitations can be revoked.");

        Status = InvitationStatus.Revoked;
        RespondedAtUtc = utcNow;
    }
}