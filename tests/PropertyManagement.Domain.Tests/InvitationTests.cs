using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;
using Xunit;

namespace PropertyManagement.Domain.Tests;

public class InvitationTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ValidOrgId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_NormalizesEmailAndStartsPending()
    {
        var invitation = Invitation.Create(
            ValidOrgId,
            "  Staff.User@Example.COM ",
            MembershipRole.PropertyManager,
            "hashed_token_123",
            FixedNow.AddDays(7),
            FixedNow);

        Assert.NotEqual(Guid.Empty, invitation.Id);
        Assert.Equal(ValidOrgId, invitation.OrganizationId);
        Assert.Equal("staff.user@example.com", invitation.Email);
        Assert.Equal(MembershipRole.PropertyManager, invitation.Role);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.Null(invitation.RespondedAtUtc);
    }

    [Fact]
    public void Create_WithPastExpiration_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Invitation.Create(
                ValidOrgId,
                "user@example.com",
                MembershipRole.Staff,
                "hash",
                FixedNow,
                FixedNow));
    }

    [Fact]
    public void Accept_WhenPendingAndNotExpired_TransitionsToAccepted()
    {
        var invitation = Invitation.Create(
            ValidOrgId,
            "user@example.com",
            MembershipRole.Staff,
            "hash",
            FixedNow.AddDays(7),
            FixedNow);

        var acceptedAt = FixedNow.AddHours(1);
        invitation.Accept(acceptedAt);

        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
        Assert.Equal(acceptedAt, invitation.RespondedAtUtc);
    }

    [Fact]
    public void Accept_WhenExpired_MarksExpiredAndThrows()
    {
        var invitation = Invitation.Create(
            ValidOrgId,
            "user@example.com",
            MembershipRole.Staff,
            "hash",
            FixedNow.AddDays(1),
            FixedNow);

        Assert.Throws<InvalidOperationException>(() =>
            invitation.Accept(FixedNow.AddDays(2)));

        Assert.Equal(InvitationStatus.Expired, invitation.Status);
    }

    [Fact]
    public void Revoke_WhenPending_TransitionsToRevoked()
    {
        var invitation = Invitation.Create(
            ValidOrgId,
            "user@example.com",
            MembershipRole.Admin,
            "hash",
            FixedNow.AddDays(7),
            FixedNow);

        var revokedAt = FixedNow.AddMinutes(30);
        invitation.Revoke(revokedAt);

        Assert.Equal(InvitationStatus.Revoked, invitation.Status);
        Assert.Equal(revokedAt, invitation.RespondedAtUtc);
    }
}
