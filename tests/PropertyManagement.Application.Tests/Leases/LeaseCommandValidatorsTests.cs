using FluentValidation.TestHelper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Repositories;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Features.Leases.AddResident;
using PropertyManagement.Application.Features.Leases.Approve;
using PropertyManagement.Application.Features.Leases.Cancel;
using PropertyManagement.Application.Features.Leases.CreateDraft;
using PropertyManagement.Application.Features.Leases.GetById;
using PropertyManagement.Application.Features.Leases.GetPaged;
using PropertyManagement.Application.Features.Leases.Renew;
using PropertyManagement.Application.Features.Leases.Submit;
using PropertyManagement.Application.Features.Leases.Terminate;
using PropertyManagement.Domain.Entities.Leases;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Application.Tests.Leases;

public sealed class LeaseCommandValidatorsTests
{
    private readonly TerminateLeaseCommandValidator _terminateValidator = new();
    private readonly RenewLeaseCommandValidator _renewValidator = new();
    private readonly AddResidentToLeaseCommandValidator _addResidentValidator = new();
    private readonly ApproveLeaseCommandValidator _approveValidator = new();
    private readonly CancelLeaseCommandValidator _cancelValidator = new();
    private readonly SubmitLeaseForApprovalCommandValidator _submitValidator = new();
    private readonly GetLeaseByIdQueryValidator _getByIdValidator = new();
    private readonly GetLeasesPagedQueryValidator _getPagedValidator = new();
    private readonly CreateDraftLeaseCommandValidator _createDraftValidator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TerminateValidator_ShouldFail_WhenReasonIsNullOrWhitespace(string? reason)
    {
        var cmd = new TerminateLeaseCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 12, 15),
            reason!,
            Guid.NewGuid());

        var result = _terminateValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Fact]
    public void TerminateValidator_ShouldFail_WhenReasonOrNotesExceedMaxLength()
    {
        var cmd = new TerminateLeaseCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 12, 15),
            new string('a', 501),
            Guid.NewGuid(),
            new string('b', 2001));

        var result = _terminateValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Reason);
        result.ShouldHaveValidationErrorFor(x => x.Notes);
    }

    [Fact]
    public void TerminateValidator_ShouldFail_WhenIdsOrDateAreEmpty()
    {
        var cmd = new TerminateLeaseCommand(Guid.Empty, Guid.Empty, default, "Mutual agreement", Guid.Empty);
        var result = _terminateValidator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.OrganizationId);
        result.ShouldHaveValidationErrorFor(x => x.LeaseId);
        result.ShouldHaveValidationErrorFor(x => x.TerminationDate);
        result.ShouldHaveValidationErrorFor(x => x.TerminatedByUserId);
    }

    [Fact]
    public void TerminateValidator_ShouldPass_WhenCommandIsValid()
    {
        var cmd = new TerminateLeaseCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 12, 15),
            "Early termination requested by tenant",
            Guid.NewGuid(),
            "Handover completed.");

        var result = _terminateValidator.TestValidate(cmd);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-500.50)]
    public void RenewValidator_ShouldFail_WhenNewRentAmountIsZeroOrNegative(decimal rentAmount)
    {
        var cmd = new RenewLeaseCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2028, 1, 1),
            NewRentAmount: rentAmount);

        var result = _renewValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.NewRentAmount);
    }

    [Fact]
    public void RenewValidator_ShouldFail_WhenNewSecurityDepositOrGracePeriodIsNegative()
    {
        var cmd = new RenewLeaseCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2028, 1, 1),
            NewSecurityDeposit: -10m,
            NewGracePeriodDays: -1);

        var result = _renewValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.NewSecurityDeposit);
        result.ShouldHaveValidationErrorFor(x => x.NewGracePeriodDays);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(29)]
    [InlineData(40)]
    public void RenewValidator_ShouldFail_WhenNewDueDayOfMonthIsOutOfRange(int dueDay)
    {
        var cmd = new RenewLeaseCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2028, 1, 1),
            NewDueDayOfMonth: dueDay);

        var result = _renewValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.NewDueDayOfMonth);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void RenewValidator_ShouldFail_WhenNewLateFeePercentageIsOutOfRange(decimal percentage)
    {
        var cmd = new RenewLeaseCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2028, 1, 1),
            NewLateFeePercentage: percentage);

        var result = _renewValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.NewLateFeePercentage);
    }

    [Fact]
    public void RenewValidator_ShouldFail_WhenNewFrequencyIsInvalidEnum()
    {
        var cmd = new RenewLeaseCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2028, 1, 1),
            NewFrequency: (RentFrequency)999);

        var result = _renewValidator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.NewFrequency);
    }

    [Fact]
    public void RenewValidator_ShouldPass_WhenOptionalTermsAreNullOrValid()
    {
        var cmdNull = new RenewLeaseCommand(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2028, 1, 1));
        _renewValidator.TestValidate(cmdNull).ShouldNotHaveAnyValidationErrors();

        var cmdValid = new RenewLeaseCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2028, 1, 1),
            NewRentAmount: 15000m,
            NewSecurityDeposit: 0m,
            NewFrequency: RentFrequency.Quarterly,
            NewDueDayOfMonth: 28,
            NewGracePeriodDays: 0,
            NewLateFeePercentage: 10m);
        _renewValidator.TestValidate(cmdValid).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AddResidentValidator_ShouldFail_WhenIdsEmptyOrRoleInvalid()
    {
        var cmd = new AddResidentToLeaseCommand(Guid.Empty, Guid.Empty, Guid.Empty, (LeaseResidentRole)99);
        var result = _addResidentValidator.TestValidate(cmd);

        result.ShouldHaveValidationErrorFor(x => x.OrganizationId);
        result.ShouldHaveValidationErrorFor(x => x.LeaseId);
        result.ShouldHaveValidationErrorFor(x => x.ResidentId);
        result.ShouldHaveValidationErrorFor(x => x.Role);
    }

    [Fact]
    public void LifecycleAndQueryValidators_ShouldFailOnInvalidInputs_AndPassOnValidInputs()
    {
        _approveValidator.TestValidate(new ApproveLeaseCommand(Guid.Empty, Guid.Empty))
            .ShouldHaveValidationErrorFor(x => x.LeaseId);
        _approveValidator.TestValidate(new ApproveLeaseCommand(Guid.NewGuid(), Guid.NewGuid()))
            .ShouldNotHaveAnyValidationErrors();

        _cancelValidator.TestValidate(new CancelLeaseCommand(Guid.Empty, Guid.Empty))
            .ShouldHaveValidationErrorFor(x => x.OrganizationId);
        _cancelValidator.TestValidate(new CancelLeaseCommand(Guid.NewGuid(), Guid.NewGuid()))
            .ShouldNotHaveAnyValidationErrors();

        _submitValidator.TestValidate(new SubmitLeaseForApprovalCommand(Guid.Empty, Guid.Empty))
            .ShouldHaveValidationErrorFor(x => x.LeaseId);
        _submitValidator.TestValidate(new SubmitLeaseForApprovalCommand(Guid.NewGuid(), Guid.NewGuid()))
            .ShouldNotHaveAnyValidationErrors();

        _getByIdValidator.TestValidate(new GetLeaseByIdQuery(Guid.Empty, Guid.Empty))
            .ShouldHaveValidationErrorFor(x => x.LeaseId);
        _getByIdValidator.TestValidate(new GetLeaseByIdQuery(Guid.NewGuid(), Guid.NewGuid()))
            .ShouldNotHaveAnyValidationErrors();

        var invalidPaged = _getPagedValidator.TestValidate(new GetLeasesPagedQuery(Guid.Empty, Status: (LeaseStatus)99, PageNumber: 0, PageSize: 200));
        invalidPaged.ShouldHaveValidationErrorFor(x => x.OrganizationId);
        invalidPaged.ShouldHaveValidationErrorFor(x => x.Status);
        invalidPaged.ShouldHaveValidationErrorFor(x => x.PageNumber);
        invalidPaged.ShouldHaveValidationErrorFor(x => x.PageSize);

        _getPagedValidator.TestValidate(new GetLeasesPagedQuery(Guid.NewGuid()))
            .ShouldNotHaveAnyValidationErrors();

        var invalidCreateDraft = _createDraftValidator.TestValidate(new CreateDraftLeaseCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 12, 1),
            new DateOnly(2027, 11, 30),
            10000m,
            20000m,
            Frequency: (RentFrequency)99));
        invalidCreateDraft.ShouldHaveValidationErrorFor(x => x.Frequency);
    }

    [Fact]
    public async Task MediatRPipeline_WhenTerminateOrRenewHasInvalidDomainValues_ShouldReturnValidationFailureInsteadOfThrowingArgumentException()
    {
        var leaseRepository = Substitute.For<ILeaseRepository>();
        var residentRepository = Substitute.For<IResidentRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton(leaseRepository);
        services.AddSingleton(residentRepository);
        services.AddSingleton(unitOfWork);
        services.AddSingleton(clock);

        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // 1. Terminate with whitespace reason -> Validation error (not ArgumentException / 500)
        var terminateResult = await sender.Send(new TerminateLeaseCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 12, 15),
            "   ",
            Guid.NewGuid()));

        Assert.True(terminateResult.IsFailure);
        Assert.Contains(terminateResult.Errors, e => e.Type == ErrorType.Validation && e.PropertyName == "Reason");

        // 2. Renew with negative rent & invalid due day -> Validation error (not ArgumentException / 500)
        var renewResult = await sender.Send(new RenewLeaseCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2028, 12, 31),
            NewRentAmount: -500m,
            NewDueDayOfMonth: 40));

        Assert.True(renewResult.IsFailure);
        Assert.Contains(renewResult.Errors, e => e.Type == ErrorType.Validation && e.PropertyName == "NewRentAmount");
        Assert.Contains(renewResult.Errors, e => e.Type == ErrorType.Validation && e.PropertyName == "NewDueDayOfMonth");

        await leaseRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenewLeasePipeline_WhenOnlyDueDayOfMonthProvided_ShouldApplyUpdatedTermsToRenewedLease()
    {
        var orgId = Guid.NewGuid();
        var utcNow = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        var activeLease = Lease.CreateDraft(
            orgId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31),
            new LeaseFinancialTerms(12000m, 24000m, RentFrequency.Monthly, dueDayOfMonth: 1, gracePeriodDays: 5, lateFeePercentage: 5m),
            utcNow);
        activeLease.AddResident(Guid.NewGuid(), LeaseResidentRole.PrimaryTenant, utcNow);
        activeLease.SubmitForApproval(utcNow);
        activeLease.Approve(utcNow);

        var leaseRepository = Substitute.For<ILeaseRepository>();
        leaseRepository.GetByIdAsync(activeLease.Id, orgId, Arg.Any<CancellationToken>()).Returns(activeLease);
        leaseRepository.HasOverlappingLeaseAsync(
            activeLease.UnitId,
            orgId,
            Arg.Any<DateOnly>(),
            Arg.Any<DateOnly>(),
            activeLease.Id,
            Arg.Any<CancellationToken>()).Returns(false);

        var unitOfWork = Substitute.For<IUnitOfWork>();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(utcNow.AddDays(1));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton(leaseRepository);
        services.AddSingleton(unitOfWork);
        services.AddSingleton(clock);

        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.Send(new RenewLeaseCommand(
            orgId,
            activeLease.Id,
            new DateOnly(2027, 12, 31),
            NewDueDayOfMonth: 15));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(15, result.Value!.DueDayOfMonth);
        Assert.Equal(12000m, result.Value.RentAmount);
    }
}
