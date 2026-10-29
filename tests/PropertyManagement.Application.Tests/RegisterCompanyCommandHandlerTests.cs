using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Email;
using PropertyManagement.Application.Abstractions.Identity;
using PropertyManagement.Application.Abstractions.Organizations;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Common.Results;
using PropertyManagement.Application.Features.Authentication.RegisterCompany;
using PropertyManagement.Domain.Entities.Organizations;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Tests;

public sealed class RegisterCompanyCommandHandlerTests
{
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IEmailConfirmationCodeIssuer _codeIssuer = Substitute.For<IEmailConfirmationCodeIssuer>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDbTransaction _transaction = Substitute.For<IDbTransaction>();
    private readonly RegisterCompanyCommandHandler _handler;
    private static readonly DateTime FixedNow = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    public RegisterCompanyCommandHandlerTests()
    {
        _clock.UtcNow.Returns(FixedNow);
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>())
            .Returns(_transaction);

        _handler = new RegisterCompanyCommandHandler(
            _identityService,
            _organizationRepository,
            _codeIssuer,
            _clock,
            _unitOfWork);
    }

    [Fact]
    public async Task Handle_ShouldReturnConflict_WhenCompanyAlreadyExists()
    {
        var command = new RegisterCompanyCommand(
            "Acme Real Estate",
            "John",
            "Doe",
            "123 Main St",
            "john@example.com",
            "Password123!",
            null);

        _organizationRepository.ExistsByNameAsync("Acme Real Estate", Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Organization.DuplicateName", result.Errors.First().Code);
        Assert.Equal(ErrorType.Conflict, result.Errors.First().Type);
        await _unitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenIdentityRegistrationFails()
    {
        var command = new RegisterCompanyCommand(
            "Acme Real Estate",
            "John",
            "Doe",
            "123 Main St",
            "john@example.com",
            "Password123!",
            null);

        _organizationRepository.ExistsByNameAsync("Acme Real Estate", Arg.Any<CancellationToken>())
            .Returns(false);

        _identityService.RegisterAsync(
            command.FirstName,
            command.LastName,
            command.Address,
            command.Email,
            command.Password,
            command.PhoneNumber,
            Arg.Any<CancellationToken>())
            .Returns(Result<IdentityUserResult>.Failure(
                new Error("Identity.PasswordTooShort", "Password is too short", ErrorType.Validation)));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Identity.PasswordTooShort", result.Errors.First().Code);
        await _transaction.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRollbackAndReturnFailure_WhenCodeIssuerFails()
    {
        var command = new RegisterCompanyCommand(
            "Acme Real Estate",
            "John",
            "Doe",
            "123 Main St",
            "john@example.com",
            "Password123!",
            null);

        var userId = Guid.NewGuid();

        _organizationRepository.ExistsByNameAsync("Acme Real Estate", Arg.Any<CancellationToken>())
            .Returns(false);
        _organizationRepository.ExistsByIdentifierAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _identityService.RegisterAsync(
            command.FirstName,
            command.LastName,
            command.Address,
            command.Email,
            command.Password,
            command.PhoneNumber,
            Arg.Any<CancellationToken>())
            .Returns(Result<IdentityUserResult>.Success(new IdentityUserResult(userId, FixedNow)));

        _codeIssuer.IssueAsync(userId, command.Email, command.FirstName, Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new Error("Email.SendingFailed", "Failed to send email", ErrorType.Failure)));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Email.SendingFailed", result.Errors.First().Code);
        await _transaction.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _transaction.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCommitAndReturnSuccess_WhenAllStepsSucceed()
    {
        var command = new RegisterCompanyCommand(
            "Acme Real Estate",
            "John",
            "Doe",
            "123 Main St",
            "john@example.com",
            "Password123!",
            null);

        var userId = Guid.NewGuid();

        _organizationRepository.ExistsByNameAsync("Acme Real Estate", Arg.Any<CancellationToken>())
            .Returns(false);
        _organizationRepository.ExistsByIdentifierAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _identityService.RegisterAsync(
            command.FirstName,
            command.LastName,
            command.Address,
            command.Email,
            command.Password,
            command.PhoneNumber,
            Arg.Any<CancellationToken>())
            .Returns(Result<IdentityUserResult>.Success(new IdentityUserResult(userId, FixedNow)));

        _codeIssuer.IssueAsync(userId, command.Email, command.FirstName, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Value.UserId);
        Assert.Equal("Acme Real Estate", result.Value.OrganizationName);
        Assert.True(result.Value.RequiresEmailConfirmation);

        await _organizationRepository.Received(1).AddAsync(Arg.Any<Organization>(), Arg.Any<CancellationToken>());
        await _organizationRepository.Received(1).AddMemberAsync(Arg.Any<OrganizationMember>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _transaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await _transaction.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }
}
