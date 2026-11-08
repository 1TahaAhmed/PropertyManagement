using NSubstitute;
using PropertyManagement.Application.Abstractions;
using PropertyManagement.Application.Abstractions.Residents;
using PropertyManagement.Application.Common.Enums;
using PropertyManagement.Application.Features.Residents.Update;
using PropertyManagement.Domain.Entities.Residents;

namespace PropertyManagement.Application.Tests;

public sealed class UpdateResidentCommandHandlerTests
{
    private readonly IResidentRepository _residentRepository = Substitute.For<IResidentRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly UpdateResidentCommandHandler _handler;
    private static readonly DateTime FixedNow = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    public UpdateResidentCommandHandlerTests()
    {
        _clock.UtcNow.Returns(FixedNow);
        _handler = new UpdateResidentCommandHandler(_residentRepository, _clock);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenResidentDoesNotExist()
    {
        var residentId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        _residentRepository.GetByIdWithContactsAsync(residentId, orgId, Arg.Any<CancellationToken>())
            .Returns((Resident?)null);

        var command = new UpdateResidentCommand(
            ResidentId: residentId,
            OrganizationId: orgId,
            FirstName: "Kareem",
            LastName: "Mostafa",
            Phone: "+201012345678");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Resident.NotFound", result.Errors.First().Code);
        Assert.Equal(ErrorType.NotFound, result.Errors.First().Type);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidation_WhenResidentIsArchived()
    {
        var orgId = Guid.NewGuid();
        var resident = Resident.Create(orgId, "Kareem", "Mostafa", "+201012345678", FixedNow);
        resident.Archive(FixedNow);

        _residentRepository.GetByIdWithContactsAsync(resident.Id, orgId, Arg.Any<CancellationToken>())
            .Returns(resident);

        var command = new UpdateResidentCommand(
            ResidentId: resident.Id,
            OrganizationId: orgId,
            FirstName: "Kareem New",
            LastName: "Mostafa",
            Phone: "+201012345678");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Resident.Archived", result.Errors.First().Code);
    }

    [Fact]
    public async Task Handle_ShouldUpdateResident_WhenValid()
    {
        var orgId = Guid.NewGuid();
        var resident = Resident.Create(orgId, "Kareem", "Mostafa", "+201012345678", FixedNow);

        _residentRepository.GetByIdWithContactsAsync(resident.Id, orgId, Arg.Any<CancellationToken>())
            .Returns(resident);

        var command = new UpdateResidentCommand(
            ResidentId: resident.Id,
            OrganizationId: orgId,
            FirstName: "Kareem Updated",
            LastName: "Mostafa New",
            Phone: "+201099999999");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Kareem Updated", result.Value.FirstName);
        Assert.Equal("Mostafa New", result.Value.LastName);
        await _residentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
