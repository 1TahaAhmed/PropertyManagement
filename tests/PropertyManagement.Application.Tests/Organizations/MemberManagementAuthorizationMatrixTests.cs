using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Features.Organizations.Members;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;
using Xunit;

namespace PropertyManagement.Application.Tests.Organizations;

public class MemberManagementAuthorizationMatrixTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public MemberManagementAuthorizationMatrixTests()
    {
        _clock.UtcNow.Returns(FixedNow);
    }

    public static IEnumerable<object?[]> ChangeMemberRoleMatrix()
    {
        // ActorRole, TargetRole, TargetIsSelf, NewRole, ActiveOwners, ExpectedAllowed, ExpectedErrorType, ExpectedErrorCode
        // Self modification is blocked for everyone (even Owner/Admin)
        yield return new object?[] { MembershipRole.Admin, MembershipRole.Admin, true, MembershipRole.Owner, 1, false, ErrorType.Forbidden, "Organization.CannotChangeOwnRole" };
        yield return new object?[] { MembershipRole.Admin, MembershipRole.Admin, true, MembershipRole.Staff, 1, false, ErrorType.Forbidden, "Organization.CannotChangeOwnRole" };
        yield return new object?[] { MembershipRole.Owner, MembershipRole.Owner, true, MembershipRole.Admin, 2, false, ErrorType.Forbidden, "Organization.CannotChangeOwnRole" };
        yield return new object?[] { MembershipRole.Owner, MembershipRole.Owner, true, MembershipRole.Owner, 1, false, ErrorType.Forbidden, "Organization.CannotChangeOwnRole" };

        // Admin cannot grant Owner to anyone
        yield return new object?[] { MembershipRole.Admin, MembershipRole.Staff, false, MembershipRole.Owner, 1, false, ErrorType.Forbidden, "Organization.OwnerRoleRestricted" };
        yield return new object?[] { MembershipRole.Admin, MembershipRole.PropertyManager, false, MembershipRole.Owner, 1, false, ErrorType.Forbidden, "Organization.OwnerRoleRestricted" };

        // Admin cannot demote/modify an Owner even when multiple Owners exist
        yield return new object?[] { MembershipRole.Admin, MembershipRole.Owner, false, MembershipRole.Admin, 2, false, ErrorType.Forbidden, "Organization.OwnerRoleRestricted" };
        yield return new object?[] { MembershipRole.Admin, MembershipRole.Owner, false, MembershipRole.Staff, 2, false, ErrorType.Forbidden, "Organization.OwnerRoleRestricted" };

        // Admin cannot modify another Admin
        yield return new object?[] { MembershipRole.Admin, MembershipRole.Admin, false, MembershipRole.PropertyManager, 1, false, ErrorType.Forbidden, "Organization.AdminCannotModifyPeerOrOwner" };
        yield return new object?[] { MembershipRole.Admin, MembershipRole.Admin, false, MembershipRole.Staff, 1, false, ErrorType.Forbidden, "Organization.AdminCannotModifyPeerOrOwner" };
        yield return new object?[] { MembershipRole.Admin, MembershipRole.Admin, false, MembershipRole.Owner, 1, false, ErrorType.Forbidden, "Organization.OwnerRoleRestricted" };

        // Admin CAN modify PropertyManager and Staff (except elevating to Owner)
        yield return new object?[] { MembershipRole.Admin, MembershipRole.Staff, false, MembershipRole.PropertyManager, 1, true, null, string.Empty };
        yield return new object?[] { MembershipRole.Admin, MembershipRole.Staff, false, MembershipRole.Admin, 1, true, null, string.Empty };
        yield return new object?[] { MembershipRole.Admin, MembershipRole.PropertyManager, false, MembershipRole.Staff, 1, true, null, string.Empty };
        yield return new object?[] { MembershipRole.Admin, MembershipRole.PropertyManager, false, MembershipRole.Admin, 1, true, null, string.Empty };

        // Owner CAN grant/revoke Owner and modify Admin/PropertyManager/Staff
        yield return new object?[] { MembershipRole.Owner, MembershipRole.Staff, false, MembershipRole.Owner, 1, true, null, string.Empty };
        yield return new object?[] { MembershipRole.Owner, MembershipRole.Admin, false, MembershipRole.Owner, 1, true, null, string.Empty };
        yield return new object?[] { MembershipRole.Owner, MembershipRole.Admin, false, MembershipRole.PropertyManager, 1, true, null, string.Empty };
        yield return new object?[] { MembershipRole.Owner, MembershipRole.Owner, false, MembershipRole.Admin, 2, true, null, string.Empty };
        yield return new object?[] { MembershipRole.Owner, MembershipRole.Owner, false, MembershipRole.Admin, 1, false, ErrorType.Validation, "Organization.LastOwnerProtected" };

        // PropertyManager and Staff cannot change any member's role
        yield return new object?[] { MembershipRole.PropertyManager, MembershipRole.Staff, false, MembershipRole.PropertyManager, 1, false, ErrorType.Forbidden, "Organization.InsufficientPermissions" };
        yield return new object?[] { MembershipRole.Staff, MembershipRole.Staff, false, MembershipRole.Admin, 1, false, ErrorType.Forbidden, "Organization.InsufficientPermissions" };
    }

    public static IEnumerable<object?[]> DeactivateMemberMatrix()
    {
        // ActorRole, TargetRole, TargetIsSelf, ActiveOwners, ExpectedAllowed, ExpectedErrorType, ExpectedErrorCode
        // Self deactivation blocked
        yield return new object?[] { MembershipRole.Admin, MembershipRole.Admin, true, 1, false, ErrorType.Forbidden, "Organization.CannotDeactivateSelf" };
        yield return new object?[] { MembershipRole.Owner, MembershipRole.Owner, true, 2, false, ErrorType.Forbidden, "Organization.CannotDeactivateSelf" };

        // Admin cannot deactivate Owner (even when multiple active Owners exist) or another Admin
        yield return new object?[] { MembershipRole.Admin, MembershipRole.Owner, false, 2, false, ErrorType.Forbidden, "Organization.OwnerRoleRestricted" };
        yield return new object?[] { MembershipRole.Admin, MembershipRole.Admin, false, 1, false, ErrorType.Forbidden, "Organization.AdminCannotModifyPeerOrOwner" };

        // Admin CAN deactivate PropertyManager and Staff
        yield return new object?[] { MembershipRole.Admin, MembershipRole.PropertyManager, false, 1, true, null, string.Empty };
        yield return new object?[] { MembershipRole.Admin, MembershipRole.Staff, false, 1, true, null, string.Empty };

        // Owner CAN deactivate another Owner (when 2+ active), Admin, PropertyManager, Staff
        yield return new object?[] { MembershipRole.Owner, MembershipRole.Owner, false, 2, true, null, string.Empty };
        yield return new object?[] { MembershipRole.Owner, MembershipRole.Owner, false, 1, false, ErrorType.Validation, "Organization.LastOwnerProtected" };
        yield return new object?[] { MembershipRole.Owner, MembershipRole.Admin, false, 1, true, null, string.Empty };
        yield return new object?[] { MembershipRole.Owner, MembershipRole.PropertyManager, false, 1, true, null, string.Empty };
        yield return new object?[] { MembershipRole.Owner, MembershipRole.Staff, false, 1, true, null, string.Empty };

        // PropertyManager and Staff cannot deactivate anyone
        yield return new object?[] { MembershipRole.PropertyManager, MembershipRole.Staff, false, 1, false, ErrorType.Forbidden, "Organization.InsufficientPermissions" };
        yield return new object?[] { MembershipRole.Staff, MembershipRole.Staff, false, 1, false, ErrorType.Forbidden, "Organization.InsufficientPermissions" };
    }

    [Theory]
    [MemberData(nameof(ChangeMemberRoleMatrix))]
    public async Task ChangeMemberRole_ShouldEnforcePermissionMatrix(
        MembershipRole actorRole,
        MembershipRole targetRole,
        bool targetIsSelf,
        MembershipRole newRole,
        int activeOwners,
        bool expectedAllowed,
        ErrorType? expectedErrorType,
        string expectedErrorCode)
    {
        var orgId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var targetUserId = targetIsSelf ? actorUserId : Guid.NewGuid();
        var member = OrganizationMember.Create(orgId, targetUserId, targetRole, FixedNow.AddDays(-1));

        _organizationRepository.GetMemberByIdAsync(member.Id, orgId, Arg.Any<CancellationToken>()).Returns(member);
        _organizationRepository.CountActiveOwnersAsync(orgId, Arg.Any<CancellationToken>()).Returns(activeOwners);

        var handler = new ChangeMemberRoleCommandHandler(_organizationRepository, _clock);
        var command = new ChangeMemberRoleCommand(orgId, member.Id, newRole, actorUserId, actorRole);

        var result = await handler.Handle(command, CancellationToken.None);

        if (expectedAllowed)
        {
            Assert.True(result.IsSuccess);
            Assert.Equal(newRole, member.Role);
            await _organizationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }
        else
        {
            Assert.True(result.IsFailure);
            var error = result.Errors.First();
            Assert.Equal(expectedErrorType, error.Type);
            Assert.Equal(expectedErrorCode, error.Code);
            Assert.Equal(targetRole, member.Role);
            await _organizationRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [MemberData(nameof(DeactivateMemberMatrix))]
    public async Task DeactivateMember_ShouldEnforcePermissionMatrix(
        MembershipRole actorRole,
        MembershipRole targetRole,
        bool targetIsSelf,
        int activeOwners,
        bool expectedAllowed,
        ErrorType? expectedErrorType,
        string expectedErrorCode)
    {
        var orgId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var targetUserId = targetIsSelf ? actorUserId : Guid.NewGuid();
        var member = OrganizationMember.Create(orgId, targetUserId, targetRole, FixedNow.AddDays(-1));

        _organizationRepository.GetMemberByIdAsync(member.Id, orgId, Arg.Any<CancellationToken>()).Returns(member);
        _organizationRepository.CountActiveOwnersAsync(orgId, Arg.Any<CancellationToken>()).Returns(activeOwners);

        var handler = new DeactivateMemberCommandHandler(_organizationRepository, _clock);
        var command = new DeactivateMemberCommand(orgId, member.Id, actorUserId, actorRole);

        var result = await handler.Handle(command, CancellationToken.None);

        if (expectedAllowed)
        {
            Assert.True(result.IsSuccess);
            Assert.Equal(MembershipStatus.Inactive, member.Status);
            await _organizationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }
        else
        {
            Assert.True(result.IsFailure);
            var error = result.Errors.First();
            Assert.Equal(expectedErrorType, error.Type);
            Assert.Equal(expectedErrorCode, error.Code);
            Assert.Equal(MembershipStatus.Active, member.Status);
            await _organizationRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public void Validators_ShouldRejectEmptyActorUserIdAndInvalidRoles()
    {
        var changeValidator = new ChangeMemberRoleCommandValidator();
        var deactivateValidator = new DeactivateMemberCommandValidator();

        var invalidChange = changeValidator.Validate(new ChangeMemberRoleCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            (MembershipRole)999,
            Guid.Empty,
            (MembershipRole)999));

        Assert.False(invalidChange.IsValid);
        Assert.Contains(invalidChange.Errors, e => e.PropertyName == nameof(ChangeMemberRoleCommand.ActorUserId));
        Assert.Contains(invalidChange.Errors, e => e.PropertyName == nameof(ChangeMemberRoleCommand.ActorRole));
        Assert.Contains(invalidChange.Errors, e => e.PropertyName == nameof(ChangeMemberRoleCommand.NewRole));

        var invalidDeactivate = deactivateValidator.Validate(new DeactivateMemberCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.Empty,
            (MembershipRole)999));

        Assert.False(invalidDeactivate.IsValid);
        Assert.Contains(invalidDeactivate.Errors, e => e.PropertyName == nameof(DeactivateMemberCommand.ActorUserId));
        Assert.Contains(invalidDeactivate.Errors, e => e.PropertyName == nameof(DeactivateMemberCommand.ActorRole));
    }
}
