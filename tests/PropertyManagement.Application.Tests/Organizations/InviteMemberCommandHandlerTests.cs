using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Authentication;
using PropertyManagement.Application.Abstractions.Email;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Features.Organizations.InviteMember;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;
using Xunit;

namespace PropertyManagement.Application.Tests.Organizations;

public class InviteMemberCommandHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IJwtTokenGenerator _jwtTokenGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDbTransaction _transaction = Substitute.For<IDbTransaction>();
    private readonly InviteMemberCommandHandler _handler;

    public InviteMemberCommandHandlerTests()
    {
        _clock.UtcNow.Returns(FixedNow);
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(_transaction);
        _handler = new InviteMemberCommandHandler(
            _organizationRepository,
            _identityService,
            _jwtTokenGenerator,
            _emailSender,
            _clock,
            _unitOfWork);
    }

    [Fact]
    public async Task Handle_ShouldCreateInvitationAndSendEmail_WhenValid()
    {
        var org = Organization.Create("Horizon Estates", FixedNow);
        var command = new InviteMemberCommand(org.Id, "New.Member@Example.com", MembershipRole.PropertyManager);

        _organizationRepository.GetByIdAsync(org.Id, Arg.Any<CancellationToken>()).Returns(org);
        _identityService.FindUserByEmailAsync("new.member@example.com", Arg.Any<CancellationToken>())
            .Returns((AuthUserResult?)null);
        _organizationRepository.GetPendingInvitationByEmailAsync(org.Id, "new.member@example.com", Arg.Any<CancellationToken>())
            .Returns((Invitation?)null);
        _jwtTokenGenerator.GenerateRefreshToken()
            .Returns(("plain-invite-token", "hashed-invite-token", FixedNow.AddDays(7)));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new.member@example.com", result.Value.Email);
        Assert.Equal(MembershipRole.PropertyManager, result.Value.Role);
        Assert.Equal(InvitationStatus.Pending, result.Value.Status);

        await _organizationRepository.Received(1).AddInvitationAsync(
            Arg.Is<Invitation>(i =>
                i.OrganizationId == org.Id &&
                i.Email == "new.member@example.com" &&
                i.TokenHash == "hashed-invite-token"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _emailSender.Received(1).SendAsync(
            "new.member@example.com",
            Arg.Is<string>(s => s.Contains(org.Name)),
            Arg.Is<string>(b => b.Contains("plain-invite-token")),
            Arg.Any<CancellationToken>());
        await _transaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenUserIsAlreadyActiveMember()
    {
        var org = Organization.Create("Horizon Estates", FixedNow);
        var userId = Guid.NewGuid();
        var member = OrganizationMember.Create(org.Id, userId, MembershipRole.Staff, FixedNow);
        var command = new InviteMemberCommand(org.Id, "member@example.com", MembershipRole.Admin);

        _organizationRepository.GetByIdAsync(org.Id, Arg.Any<CancellationToken>()).Returns(org);
        _identityService.FindUserByEmailAsync("member@example.com", Arg.Any<CancellationToken>())
            .Returns(new AuthUserResult(userId, "member@example.com", "Ali", "Hassan"));
        _organizationRepository.GetMemberAsync(org.Id, userId, Arg.Any<CancellationToken>())
            .Returns(member);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Organization.AlreadyMember", result.Errors.First().Code);
        Assert.Equal(ErrorType.Conflict, result.Errors.First().Type);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenPendingInvitationAlreadyExists()
    {
        var org = Organization.Create("Horizon Estates", FixedNow);
        var existingInvitation = Invitation.Create(
            org.Id,
            "pending@example.com",
            MembershipRole.Staff,
            "hash",
            FixedNow.AddDays(5),
            FixedNow.AddDays(-2));
        var command = new InviteMemberCommand(org.Id, "pending@example.com", MembershipRole.Staff);

        _organizationRepository.GetByIdAsync(org.Id, Arg.Any<CancellationToken>()).Returns(org);
        _identityService.FindUserByEmailAsync("pending@example.com", Arg.Any<CancellationToken>())
            .Returns((AuthUserResult?)null);
        _organizationRepository.GetPendingInvitationByEmailAsync(org.Id, "pending@example.com", Arg.Any<CancellationToken>())
            .Returns(existingInvitation);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Organization.DuplicateInvitation", result.Errors.First().Code);
    }

    [Fact]
    public async Task Handle_ShouldRollbackTransaction_WhenEmailSendingFails()
    {
        var org = Organization.Create("Horizon Estates", FixedNow);
        var command = new InviteMemberCommand(org.Id, "member@example.com", MembershipRole.Staff);

        _organizationRepository.GetByIdAsync(org.Id, Arg.Any<CancellationToken>()).Returns(org);
        _identityService.FindUserByEmailAsync("member@example.com", Arg.Any<CancellationToken>())
            .Returns((AuthUserResult?)null);
        _organizationRepository.GetPendingInvitationByEmailAsync(org.Id, "member@example.com", Arg.Any<CancellationToken>())
            .Returns((Invitation?)null);
        _jwtTokenGenerator.GenerateRefreshToken()
            .Returns(("plain-token", "hashed-token", FixedNow.AddDays(7)));
        _emailSender.SendAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Organization.InvitationEmailFailed", result.Errors.First().Code);
        await _transaction.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _transaction.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_ShouldRejectOwnerRole()
    {
        var validator = new InviteMemberCommandValidator();
        var command = new InviteMemberCommand(Guid.NewGuid(), "user@example.com", MembershipRole.Owner);

        var validation = validator.Validate(command);

        Assert.False(validation.IsValid);
    }
}
